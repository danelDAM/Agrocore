using System;
using System.Collections.Generic;
using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Security
{
    /// <summary>Saioa hasteko saiakera baten emaitza.</summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public User User { get; set; }
        public Farm Farm { get; set; }
    }

    /// <summary>
    /// Kredentzialak egiaztatzen ditu. Urratsak (zerbitzarian, inoiz ez WPFan):
    ///   1. Erabiltzailea bilatzen du.
    ///   2. Pasahitza egiaztatzen du (PBKDF2 hasha).
    ///   3. Erabiltzailea aktibo dagoela egiaztatzen du.
    ///   4. Rola egiaztatzen du.
    ///   5. Bere ustiategia existitzen dela egiaztatzen du.
    /// Gainera, huts egindako hainbat saiakeraren ondoren, erabiltzailea denbora batez blokeatzen du (indar gordinaren aurka).
    /// </summary>
    public class AuthService
    {
        private const string GenericError = "Erabiltzailea edo pasahitza okerrak dira";

        private readonly DataStore store;
        private readonly ConfigStore config;
        private readonly EventLogger logger;

        // Erabiltzaile bakoitzaren huts egindako saiakerak. Hainbat harik erabiltzen dute aldi berean: lock-ekin babesten da.
        private readonly Dictionary<string, int> failedAttempts = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> lockedUntil = new Dictionary<string, DateTime>();
        private readonly object attemptsLock = new object();

        // Edozein pasahitzen hasha, erabiltzailea existitu ez arren denbora bera behar izateko
        // (horrela erasotzaile batek ezin du jakin zein erabiltzaile dauden erantzun-denbora neurtuz).
        private readonly string dummyHash = PasswordHasher.Hash("dummy-password");

        public AuthService(DataStore store, ConfigStore config, EventLogger logger)
        {
            this.store = store;
            this.config = config;
            this.logger = logger;
        }

        public LoginResult Login(string username, string password, string remote)
        {
            LoginResult result = new LoginResult();
            username = (username ?? "").Trim();
            string key = username.ToLower();

            // 0. Saiakera gehiegirengatik blokeatuta dago?
            lock (attemptsLock)
            {
                DateTime until;
                if (lockedUntil.TryGetValue(key, out until) && until > DateTime.Now)
                {
                    int seconds = (int)(until - DateTime.Now).TotalSeconds + 1;
                    result.Message = "Saiakera gehiegi huts egin dira. Itxaron " + seconds + " segundo.";
                    logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                        "Saio-hasiera blokeatuta '" + username + "' erabiltzailearentzat (" + remote + ")");
                    return result;
                }
            }

            // 1. Erabiltzailea bilatu
            User user = store.FindUserByUsername(username);

            // 2. Pasahitza egiaztatu
            bool passwordOk;
            if (user == null)
            {
                PasswordHasher.Verify(password, dummyHash); // kalkulu-denbora bera
                passwordOk = false;
            }
            else
            {
                passwordOk = PasswordHasher.Verify(password, user.PasswordHash);
            }

            if (!passwordOk)
            {
                RegisterFailure(key);
                result.Message = GenericError;
                logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                    "Saio-hasierak huts egin du '" + username + "' erabiltzailearentzat (" + remote + ")");
                return result;
            }

            // 3. Erabiltzaile aktiboa
            if (!user.IsActive)
            {
                result.Message = "Erabiltzailea desaktibatuta dago. Jarri harremanetan administratzailearekin.";
                logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                    "Desaktibatutako erabiltzailearen saio-hasiera: '" + username + "'", user.FarmId, null);
                return result;
            }

            // 4. Rol baliozkoa
            if (!Enum.IsDefined(typeof(UserRole), user.Role))
            {
                result.Message = "Erabiltzaileak ez du rol baliozkorik.";
                return result;
            }

            // 5. Ustiategia (administratzaileak ez du behar)
            Farm farm = null;
            if (user.Role != UserRole.Admin)
            {
                farm = store.GetFarm(user.FarmId);
                if (farm == null)
                {
                    result.Message = "Erabiltzaileak ez du ustiategirik esleituta.";
                    logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                        "'" + username + "' erabiltzailearen saio-hasiera, ustiategi baliozkorik gabe");
                    return result;
                }
            }

            lock (attemptsLock)
            {
                failedAttempts.Remove(key);
                lockedUntil.Remove(key);
            }

            store.SetLastLogin(user.Id);
            result.Success = true;
            result.User = user;
            result.Farm = farm;
            return result;
        }

        private void RegisterFailure(string key)
        {
            lock (attemptsLock)
            {
                int count;
                failedAttempts.TryGetValue(key, out count);
                count++;
                failedAttempts[key] = count;

                if (count >= config.Current.MaxLoginAttempts)
                {
                    lockedUntil[key] = DateTime.Now.AddSeconds(config.Current.LockoutSeconds);
                    failedAttempts[key] = 0;
                }
            }
        }

        /// <summary>Pasahitz-aldaketa: zerbitzariak uneko pasahitza eskatzen du.</summary>
        public string ChangePassword(int userId, string currentPassword, string newPassword)
        {
            User user = store.GetUser(userId);
            if (user == null)
            {
                return "Ez da erabiltzailea aurkitu";
            }
            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            {
                return "Uneko pasahitza ez da zuzena";
            }
            string error = ValidatePassword(newPassword);
            if (error != null)
            {
                return error;
            }
            if (currentPassword == newPassword)
            {
                return "Pasahitz berriak unekoaren desberdina izan behar du";
            }
            store.SetPassword(userId, newPassword);
            return null; // null = errorerik gabe
        }

        /// <summary>Pasahitzaren gutxieneko arauak. null itzultzen du baliozkoa bada.</summary>
        public static string ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                return "Pasahitzak gutxienez 6 karaktere izan behar ditu";
            }
            bool hasLetter = false;
            bool hasDigit = false;
            foreach (char c in password)
            {
                if (char.IsLetter(c)) hasLetter = true;
                if (char.IsDigit(c)) hasDigit = true;
            }
            if (!hasLetter || !hasDigit)
            {
                return "Pasahitzak letrak eta zenbakiak izan behar ditu";
            }
            return null;
        }
    }
}
