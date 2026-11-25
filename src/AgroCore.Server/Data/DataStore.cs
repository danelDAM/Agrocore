using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgroCore.Server.Security;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;
using AgroCore.Shared.Networking;

namespace AgroCore.Server.Data
{
    /// <summary>
    /// Zerbitzariaren datu-biltegia: erabiltzaileak, ustiategiak, alta emandako makinak eta soroak.
    ///
    /// Hari askok (bat konektatutako bezero bakoitzeko) aldi berean erabil dezakete; horregatik, metodo
    /// GUZTIEK lock (dataLock) barruan egiten dute lan. Horrela, ez dira inoiz bi hari egongo
    /// zerrendak aldi berean aldatzen (lasterketa-baldintza).
    /// </summary>
    public class DataStore
    {
        private readonly string path;
        private readonly object dataLock = new object();
        private DatabaseFile db;

        public DataStore(string path)
        {
            this.path = path;
        }

        /// <summary>Fitxategia kargatzen du. Existitzen ez bada, hasierako adibide-datuak sortzen ditu.</summary>
        public void Load()
        {
            lock (dataLock)
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    db = JsonSerializer.Deserialize<DatabaseFile>(json, MessageSerializer.FileOptions);
                }
                if (db == null)
                {
                    db = new DatabaseFile();
                    SeedDemoData();
                }
                SaveInternal();
            }
        }

        public void Save()
        {
            lock (dataLock)
            {
                SaveInternal();
            }
        }

        private void SaveInternal()
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(db, MessageSerializer.FileOptions);
            // Lehenik aldi baterako fitxategi batean idazten da eta gero ordezkatu egiten da: programa
            // idazten ari dela ixten bada, fitxategi ona ez da galtzen.
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, true);
        }

        // ==================================================================
        // ERABILTZAILEAK
        // ==================================================================

        public User FindUserByUsername(string username)
        {
            lock (dataLock)
            {
                foreach (User user in db.Users)
                {
                    if (string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        return user;
                    }
                }
                return null;
            }
        }

        public User GetUser(int userId)
        {
            lock (dataLock)
            {
                foreach (User user in db.Users)
                {
                    if (user.Id == userId)
                    {
                        return user;
                    }
                }
                return null;
            }
        }

        public List<User> GetUsers()
        {
            lock (dataLock)
            {
                return new List<User>(db.Users);
            }
        }

        public User AddUser(string username, string password, string fullName, UserRole role, int farmId)
        {
            lock (dataLock)
            {
                User user = new User();
                user.Id = db.NextUserId;
                db.NextUserId++;
                user.Username = username;
                user.PasswordHash = PasswordHasher.Hash(password);
                user.FullName = fullName;
                user.Role = role;
                user.FarmId = farmId;
                user.IsActive = true;
                db.Users.Add(user);
                SaveInternal();
                return user;
            }
        }

        public void SetPassword(int userId, string newPassword)
        {
            string hash = PasswordHasher.Hash(newPassword); // lock-etik kanpo kalkulatzen da, motela delako
            lock (dataLock)
            {
                User user = FindUserById(userId);
                if (user != null)
                {
                    user.PasswordHash = hash;
                    SaveInternal();
                }
            }
        }

        public void SetUserActive(int userId, bool active)
        {
            lock (dataLock)
            {
                User user = FindUserById(userId);
                if (user != null)
                {
                    user.IsActive = active;
                    SaveInternal();
                }
            }
        }

        public void SetFullName(int userId, string fullName)
        {
            lock (dataLock)
            {
                User user = FindUserById(userId);
                if (user != null)
                {
                    user.FullName = fullName;
                    SaveInternal();
                }
            }
        }

        public void SetUserFarm(int userId, int farmId)
        {
            lock (dataLock)
            {
                User user = FindUserById(userId);
                if (user != null)
                {
                    user.FarmId = farmId;
                    SaveInternal();
                }
            }
        }

        public void SetLastLogin(int userId)
        {
            lock (dataLock)
            {
                User user = FindUserById(userId);
                if (user != null)
                {
                    user.LastLogin = DateTime.Now;
                    SaveInternal();
                }
            }
        }

        public void DeleteUser(int userId)
        {
            lock (dataLock)
            {
                db.Users.RemoveAll(u => u.Id == userId);
                SaveInternal();
            }
        }

        private User FindUserById(int userId)
        {
            foreach (User user in db.Users)
            {
                if (user.Id == userId)
                {
                    return user;
                }
            }
            return null;
        }

        // ==================================================================
        // USTIATEGIAK
        // ==================================================================

        public Farm GetFarm(int farmId)
        {
            lock (dataLock)
            {
                foreach (Farm farm in db.Farms)
                {
                    if (farm.Id == farmId)
                    {
                        return farm;
                    }
                }
                return null;
            }
        }

        public List<Farm> GetFarms()
        {
            lock (dataLock)
            {
                return new List<Farm>(db.Farms);
            }
        }

        /// <summary>
        /// Ustiategi oso bat sortzen du: baserria, bere soroak, bere makinak eta bere kargagailuak.
        /// Makinek Seeder-01, Irrigator-01... izena dute, eta haien Id globalak "F&lt;farmId&gt;-" darama aurretik.
        /// </summary>
        public Farm CreateFarm(string name, int ownerId, int fieldCount, int seeders, int irrigators,
                               int sprayers, int harvesters, int chargers, string fleetKey)
        {
            lock (dataLock)
            {
                Farm farm = new Farm();
                farm.Id = db.NextFarmId;
                db.NextFarmId++;
                farm.Name = name;
                farm.OwnerId = ownerId;
                farm.FieldCount = Math.Clamp(fieldCount, 1, 9);
                farm.ChargerCount = Math.Clamp(chargers, 1, 6);
                farm.FleetKeyHash = PasswordHasher.Hash(fleetKey);
                db.Farms.Add(farm);

                for (int i = 0; i < farm.FieldCount; i++)
                {
                    Field field = new Field();
                    field.Id = FarmLayout.FieldId(i);
                    field.Name = FarmLayout.FieldName(i);
                    field.FarmId = farm.Id;
                    field.Index = i;
                    field.Status = FieldStatus.Empty;
                    db.Fields.Add(field);
                }

                int slot = 0;
                slot = AddMachines(farm.Id, MachineType.Seeder, "Seeder", seeders, slot);
                slot = AddMachines(farm.Id, MachineType.Irrigator, "Irrigator", irrigators, slot);
                slot = AddMachines(farm.Id, MachineType.Sprayer, "Sprayer", sprayers, slot);
                AddMachines(farm.Id, MachineType.Harvester, "Harvester", harvesters, slot);

                SaveInternal();
                return farm;
            }
        }

        private int AddMachines(int farmId, MachineType type, string baseName, int count, int firstSlot)
        {
            int slot = firstSlot;
            for (int i = 1; i <= count; i++)
            {
                MachineRegistration machine = new MachineRegistration();
                machine.Name = baseName + "-" + i.ToString("00");
                machine.Id = "F" + farmId + "-" + machine.Name;
                machine.FarmId = farmId;
                machine.Type = type;
                machine.SlotIndex = slot;
                db.Machines.Add(machine);
                slot++;
            }
            return slot;
        }

        public void SetFarmOwner(int farmId, int ownerId)
        {
            lock (dataLock)
            {
                foreach (Farm farm in db.Farms)
                {
                    if (farm.Id == farmId)
                    {
                        farm.OwnerId = ownerId;
                    }
                }
                SaveInternal();
            }
        }

        public void RenameFarm(int farmId, string newName)
        {
            lock (dataLock)
            {
                foreach (Farm farm in db.Farms)
                {
                    if (farm.Id == farmId)
                    {
                        farm.Name = newName;
                    }
                }
                SaveInternal();
            }
        }

        public void SetFleetKey(int farmId, string fleetKey)
        {
            string hash = PasswordHasher.Hash(fleetKey);
            lock (dataLock)
            {
                foreach (Farm farm in db.Farms)
                {
                    if (farm.Id == farmId)
                    {
                        farm.FleetKeyHash = hash;
                    }
                }
                SaveInternal();
            }
        }

        /// <summary>Ustiategia eta haren mendeko guztia ezabatzen du (soroak, makinak, lotutako erabiltzaileak).</summary>
        public void DeleteFarm(int farmId)
        {
            lock (dataLock)
            {
                db.Farms.RemoveAll(f => f.Id == farmId);
                db.Fields.RemoveAll(f => f.FarmId == farmId);
                db.Machines.RemoveAll(m => m.FarmId == farmId);
                db.Users.RemoveAll(u => u.FarmId == farmId && u.Role != UserRole.Admin);
                SaveInternal();
            }
        }

        // ==================================================================
        // MAKINAK ETA SOROAK
        // ==================================================================

        public List<MachineRegistration> GetMachines(int farmId)
        {
            lock (dataLock)
            {
                List<MachineRegistration> result = new List<MachineRegistration>();
                foreach (MachineRegistration machine in db.Machines)
                {
                    if (machine.FarmId == farmId)
                    {
                        result.Add(machine);
                    }
                }
                return result;
            }
        }

        public MachineRegistration GetMachine(string machineId)
        {
            lock (dataLock)
            {
                foreach (MachineRegistration machine in db.Machines)
                {
                    if (machine.Id == machineId)
                    {
                        return machine;
                    }
                }
                return null;
            }
        }

        /// <summary>Ustiategi baten gordetako soroen kopia.</summary>
        public List<Field> GetFields(int farmId)
        {
            lock (dataLock)
            {
                List<Field> result = new List<Field>();
                foreach (Field field in db.Fields)
                {
                    if (field.FarmId == farmId)
                    {
                        result.Add(field.Clone());
                    }
                }
                return result;
            }
        }

        /// <summary>Ustiategi baten soroen uneko egoera gordetzen du.</summary>
        public void SaveFields(int farmId, List<Field> fields)
        {
            lock (dataLock)
            {
                db.Fields.RemoveAll(f => f.FarmId == farmId);
                foreach (Field field in fields)
                {
                    db.Fields.Add(field.Clone());
                }
                SaveInternal();
            }
        }

        public MachineStatistics GetMachineStats(string machineId)
        {
            lock (dataLock)
            {
                MachineStatistics stats;
                if (db.MachineStats.TryGetValue(machineId, out stats))
                {
                    return stats;
                }
                return null;
            }
        }

        public void SaveMachineStats(List<MachineStatistics> statsList)
        {
            lock (dataLock)
            {
                foreach (MachineStatistics stats in statsList)
                {
                    db.MachineStats[stats.MachineId] = stats;
                }
                SaveInternal();
            }
        }

        // ==================================================================
        // HASIERAKO ADIBIDE-DATUAK
        // ==================================================================

        /// <summary>
        /// Administratzailea eta adibideko hiru nekazari sortzen ditu, beren ustiategiekin.
        /// Demoaren pasahitzak eta flota-gakoak: ikus README.
        /// </summary>
        private void SeedDemoData()
        {
            Console.WriteLine("Hasierako adibide-datuak sortzen...");

            AddUserNoSave("admin", "admin123", "AGROCORE administratzailea", UserRole.Admin, 0);

            // Juan García - Esperantza baserria: 6 soro, 11 makina, 3 kargagailu
            User juan = AddUserNoSave("juan", "juan123", "Juan García", UserRole.Farmer, 0);
            Farm esperanza = CreateFarmNoSave("Esperantza baserria", juan.Id, 6, 2, 2, 3, 4, 3, "AGRO-DEMO-1");
            juan.FarmId = esperanza.Id;
            PlanSeedCrops(esperanza.Id, new CropType[] { CropType.Tomato, CropType.Corn, CropType.Carrot, CropType.Carrot, CropType.Corn, CropType.Tomato });

            // María López - Olibondoak baserria: 4 soro, 7 makina, 2 kargagailu
            User maria = AddUserNoSave("maria", "maria123", "María López", UserRole.Farmer, 0);
            Farm olivos = CreateFarmNoSave("Olibondoak baserria", maria.Id, 4, 1, 2, 2, 2, 2, "AGRO-DEMO-2");
            maria.FarmId = olivos.Id;
            PlanSeedCrops(olivos.Id, new CropType[] { CropType.Corn, CropType.Carrot, CropType.Tomato, CropType.Carrot });

            // Pedro Ruiz - Haritzdi baserria: 3 soro, 5 makina, 2 kargagailu
            User pedro = AddUserNoSave("pedro", "pedro123", "Pedro Ruiz", UserRole.Farmer, 0);
            Farm robledal = CreateFarmNoSave("Haritzdi baserria", pedro.Id, 3, 1, 1, 1, 2, 2, "AGRO-DEMO-3");
            pedro.FarmId = robledal.Id;
            PlanSeedCrops(robledal.Id, new CropType[] { CropType.Carrot, CropType.Corn, CropType.Tomato });

            // Juanen ustiategiko operadorea eta behatzailea
            AddUserNoSave("operador", "operador123", "Luis Martín (operadorea)", UserRole.Operator, esperanza.Id);
            AddUserNoSave("visor", "visor123", "Ana Torres (behatzailea)", UserRole.Viewer, esperanza.Id);
        }

        private User AddUserNoSave(string username, string password, string fullName, UserRole role, int farmId)
        {
            User user = new User();
            user.Id = db.NextUserId;
            db.NextUserId++;
            user.Username = username;
            user.PasswordHash = PasswordHasher.Hash(password);
            user.FullName = fullName;
            user.Role = role;
            user.FarmId = farmId;
            db.Users.Add(user);
            return user;
        }

        private Farm CreateFarmNoSave(string name, int ownerId, int fields, int seeders, int irrigators,
                                      int sprayers, int harvesters, int chargers, string fleetKey)
        {
            // CreateFarm-ek lock(dataLock) erabiltzen du; C#-ko lock-a berriro sar daitekeena da (hari bera berriro
            // sar daiteke), beraz hemen dei daiteke, lock-aren barruan egon arren.
            return CreateFarm(name, ownerId, fields, seeders, irrigators, sprayers, harvesters, chargers, fleetKey);
        }

        private void PlanSeedCrops(int farmId, CropType[] crops)
        {
            foreach (Field field in db.Fields)
            {
                if (field.FarmId == farmId && field.Index < crops.Length)
                {
                    field.PlannedCrop = crops[field.Index];
                    field.AutoReplant = true;
                }
            }
        }
    }
}
