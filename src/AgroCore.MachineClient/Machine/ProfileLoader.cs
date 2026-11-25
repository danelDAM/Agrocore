using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Lee las capacidades de cada máquina de machine-profiles.json.
    /// Si una máquina no aparece en el archivo, usa valores por defecto según su tipo.
    /// </summary>
    public static class ProfileLoader
    {
        public static MachineProfile Load(string machineName, MachineType type)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "machine-profiles.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    Dictionary<string, MachineProfile> profiles =
                        JsonSerializer.Deserialize<Dictionary<string, MachineProfile>>(json, MessageSerializer.Options);
                    MachineProfile profile;
                    if (profiles != null && profiles.TryGetValue(machineName, out profile))
                    {
                        return profile;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("No se pudo leer machine-profiles.json: " + ex.Message);
                }
            }
            return DefaultFor(type);
        }

        public static MachineProfile DefaultFor(MachineType type)
        {
            MachineProfile profile = new MachineProfile();
            switch (type)
            {
                case MachineType.Seeder:
                    profile.Model = "AgroSeed S300";
                    profile.Speed = 4.2;
                    profile.Capacity = 70;
                    profile.WorkRadius = 1.6;
                    break;
                case MachineType.Irrigator:
                    profile.Model = "HydroRun 1500";
                    profile.Speed = 3.8;
                    profile.Capacity = 1500;
                    profile.WorkRadius = 2.0;
                    profile.MoveConsumption = 0.27;
                    break;
                case MachineType.Sprayer:
                    profile.Model = "SkySpray D4";
                    profile.Speed = 6.0;
                    profile.Capacity = 30;
                    profile.WorkRadius = 1.5;
                    profile.MoveConsumption = 0.33;
                    profile.WorkConsumption = 0.5;
                    break;
                case MachineType.Harvester:
                    profile.Model = "GrainMaster H5";
                    profile.Speed = 4.5;
                    profile.Capacity = 120;
                    profile.WorkRadius = 1.6;
                    profile.MoveConsumption = 0.27;
                    profile.WorkConsumption = 0.47;
                    break;
            }
            return profile;
        }
    }
}
