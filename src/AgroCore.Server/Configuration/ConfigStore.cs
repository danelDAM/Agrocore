using System;
using System.IO;
using System.Text.Json;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Configuration
{
    /// <summary>
    /// Zerbitzariaren konfigurazioa kargatu eta gordetzen du (server-config.json).
    /// Fitxategia existitzen ez bada, balio lehenetsiekin sortzen da.
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

        /// <summary>Uneko konfigurazioa. Objektua bera itzultzen da: ez aldatu zuzenean, erabili Update.</summary>
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
                    Console.WriteLine("Ezin izan da " + path + " irakurri: " + ex.Message + ". Balio lehenetsiak erabiltzen dira.");
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
                // Atakak zerbitzaria berrabiaraztean bakarrik aldatzen dira: unekoak mantentzen dira.
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
