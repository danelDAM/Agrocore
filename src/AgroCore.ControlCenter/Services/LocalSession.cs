using System;
using System.IO;
using System.Text.Json;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>Kontrol Zentroak erabiltzailearen ordenagailuan gordetzen dituen datuak.</summary>
    public class LocalSessionData
    {
        public string Host { get; set; } = NetworkDefaults.Host;
        public string LastUsername { get; set; } = "";

        // Saio aktiboa (saioa ixtean ezabatzen da)
        public string Username { get; set; }
        public string FullName { get; set; }
        public string FarmName { get; set; }
        public DateTime? LoginTime { get; set; }
    }

    /// <summary>
    /// Saio lokala: %APPDATA%\AgroCore\session.json fitxategian gordetzen da.
    /// - Saioa hastean erabiltzailearen datuak gordetzen dira.
    /// - SAIOA IXTEAN ezabatzen dira (zerbitzaria eta azken erabiltzailea bakarrik gogoratzen dira saioa hasteko).
    /// Pasahitza eta tokena ez dira inoiz gordetzen.
    /// </summary>
    public static class LocalSession
    {
        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgroCore");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "session.json");
            }
        }

        public static LocalSessionData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    LocalSessionData data = JsonSerializer.Deserialize<LocalSessionData>(File.ReadAllText(FilePath), MessageSerializer.FileOptions);
                    if (data != null)
                    {
                        return data;
                    }
                }
            }
            catch (Exception)
            {
                // fitxategia hondatuta: hutsetik hasten da
            }
            return new LocalSessionData();
        }

        public static void Save(LocalSessionData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(data, MessageSerializer.FileOptions));
            }
            catch (Exception)
            {
                // ez da larria gorde ezin bada
            }
        }

        /// <summary>Saio aktiboaren datuak ezabatzen ditu (logout).</summary>
        public static void Clear()
        {
            LocalSessionData data = Load();
            data.Username = null;
            data.FullName = null;
            data.FarmName = null;
            data.LoginTime = null;
            Save(data);
        }
    }
}
