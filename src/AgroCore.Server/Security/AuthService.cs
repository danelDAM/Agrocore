using System;
using System.Collections.Generic;
using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Security
{
    /// <summary>Resultado de un intento de login.</summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public User User { get; set; }
        public Farm Farm { get; set; }
    }

    /// <summary>
    /// Comprueba las credenciales. Pasos (en el servidor, nunca en el WPF):
    ///   1. Busca el usuario.
    ///   2. Comprueba la contraseña (hash PBKDF2).
    ///   3. Comprueba que el usuario esté activo.
    ///   4. Comprueba el rol.
    ///   5. Comprueba que su explotación exista.
    /// Además bloquea un rato el usuario tras varios intentos fallidos (contra fuerza bruta).
    /// </summary>
    public class AuthService
    {
        private const string GenericError = "Usuario o contraseña incorrectos";

        private readonly DataStore store;
        private readonly ConfigStore config;
        private readonly EventLogger logger;

        // Intentos fallidos por usuario. Lo usan varios hilos a la vez: se protege con lock.
        private readonly Dictionary<string, int> failedAttempts = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> lockedUntil = new Dictionary<string, DateTime>();
        private readonly object attemptsLock = new object();

        // Hash de una contraseña cualquiera, para tardar lo mismo aunque el usuario no exista
        // (así un atacante no puede saber qué usuarios existen midiendo el tiempo de respuesta).
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

            // 0. ¿Está bloqueado por demasiados intentos?
            lock (attemptsLock)
            {
                DateTime until;
                if (lockedUntil.TryGetValue(key, out until) && until > DateTime.Now)
                {
                    int seconds = (int)(until - DateTime.Now).TotalSeconds + 1;
                    result.Message = "Demasiados intentos fallidos. Espere " + seconds + " segundos.";
                    logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                        "Login bloqueado para '" + username + "' desde " + remote);
                    return result;
                }
            }

            // 1. Buscar usuario
            User user = store.FindUserByUsername(username);

            // 2. Comprobar contraseña
            bool passwordOk;
            if (user == null)
            {
                PasswordHasher.Verify(password, dummyHash); // mismo tiempo de cálculo
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
                    "Login fallido para '" + username + "' desde " + remote);
                return result;
            }

            // 3. Usuario activo
            if (!user.IsActive)
            {
                result.Message = "El usuario está desactivado. Contacte con el administrador.";
                logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                    "Login de usuario desactivado '" + username + "'", user.FarmId, null);
                return result;
            }

            // 4. Rol válido
            if (!Enum.IsDefined(typeof(UserRole), user.Role))
            {
                result.Message = "El usuario no tiene un rol válido.";
                return result;
            }

            // 5. Explotación (el administrador no necesita una)
            Farm farm = null;
            if (user.Role != UserRole.Admin)
            {
                farm = store.GetFarm(user.FarmId);
                if (farm == null)
                {
                    result.Message = "El usuario no tiene ninguna explotación asignada.";
                    logger.Log(EventKind.LoginFailed, LogSeverity.Security,
                        "Login de '" + username + "' sin explotación válida");
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

        /// <summary>Cambio de contraseña: el servidor exige la contraseña actual.</summary>
        public string ChangePassword(int userId, string currentPassword, string newPassword)
        {
            User user = store.GetUser(userId);
            if (user == null)
            {
                return "Usuario no encontrado";
            }
            if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            {
                return "La contraseña actual no es correcta";
            }
            string error = ValidatePassword(newPassword);
            if (error != null)
            {
                return error;
            }
            if (currentPassword == newPassword)
            {
                return "La nueva contraseña debe ser distinta de la actual";
            }
            store.SetPassword(userId, newPassword);
            return null; // null = sin errores
        }

        /// <summary>Reglas mínimas de contraseña. Devuelve null si es válida.</summary>
        public static string ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                return "La contraseña debe tener al menos 6 caracteres";
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
                return "La contraseña debe tener letras y números";
            }
            return null;
        }
    }
}
