using System;
using System.IO;
using System.Text.Json;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Configuration
{
    /// <summary>
    /// Carga y guarda la configuración del servidor (server-config.json).
    /// Si el archivo no existe, se crea con los valores por defecto.
    /// </summary>
    public class ConfigStore
    {
        private readonly string path;
        private readonly object configLock = new object();
        private SystemConfig config;

        public ConfigStore(string path)
        {
            this.path = path;
            Load();
        }

        /// <summary>Configuración actual. Se devuelve el objeto: no modificarlo directamente, usar Update.</summary>
        public SystemConfig Current
        {
            get
            {
                lock (configLock)
                {
                    return config;
                }
            }
        }

        private void Load()
        {
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    config = JsonSerializer.Deserialize<SystemConfig>(json, MessageSerializer.FileOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("No se pudo leer " + path + ": " + ex.Message + ". Se usan valores por defecto.");
                }
            }

            if (config == null)
            {
                config = new SystemConfig();
                Save();
            }
        }

        public void Update(SystemConfig newConfig)
        {
            lock (configLock)
            {
                // Los puertos solo cambian al reiniciar el servidor: se conservan los actuales.
                newConfig.TcpPort = config.TcpPort;
                newConfig.UdpPort = config.UdpPort;
                config = newConfig;
            }
            Save();
        }

        private void Save()
        {
            lock (configLock)
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonSerializer.Serialize(config, MessageSerializer.FileOptions));
            }
        }
    }
}
