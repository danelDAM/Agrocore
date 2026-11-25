using System;
using System.IO;
using System.Text.Json;
using AgroCore.Shared.Networking;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>Datos que el Centro de Control guarda en el equipo del usuario.</summary>
    public class LocalSessionData
    {
        public string Host { get; set; } = NetworkDefaults.Host;
        public string LastUsername { get; set; } = "";

        // Sesión activa (se borra al cerrar sesión)
        public string Username { get; set; }
        public string FullName { get; set; }
        public string FarmName { get; set; }
        public DateTime? LoginTime { get; set; }
    }

    /// <summary>
    /// Sesión local: se guarda en %APPDATA%\AgroCore\session.json.
    /// - Al iniciar sesión se guardan los datos del usuario.
    /// - Al CERRAR SESIÓN se eliminan (solo se recuerda el servidor y el último usuario para el login).
    /// Nunca se guarda la contraseña ni el token.
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
                // archivo dañado: se empieza de cero
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
                // no es grave si no se puede guardar
            }
        }

        /// <summary>Elimina los datos de la sesión activa (logout).</summary>
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
