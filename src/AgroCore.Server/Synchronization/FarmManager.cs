using System.Collections.Concurrent;
using System.Collections.Generic;
using AgroCore.Server.Charging;
using AgroCore.Server.Collision;
using AgroCore.Server.Configuration;
using AgroCore.Server.Data;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Server.Tasks;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Synchronization
{
    /// <summary>Todas las explotaciones en marcha en el servidor (clave = FarmId).</summary>
    public class FarmManager
    {
        private readonly ConcurrentDictionary<int, FarmRuntime> farms = new ConcurrentDictionary<int, FarmRuntime>();
        private readonly DataStore store;
        private readonly ConfigStore config;
        private readonly EventLogger logger;

        public FarmManager(DataStore store, ConfigStore config, EventLogger logger)
        {
            this.store = store;
            this.config = config;
            this.logger = logger;
        }

        public void LoadAll()
        {
            foreach (Farm farm in store.GetFarms())
            {
                AddFarm(farm);
            }
        }

        public void AddFarm(Farm farm)
        {
            List<MachineRegistration> machines = store.GetMachines(farm.Id);

            FarmRuntime runtime = new FarmRuntime();
            runtime.Farm = farm;
            User owner = store.GetUser(farm.OwnerId);
            runtime.OwnerName = owner != null ? owner.FullName : "";
            runtime.Layout = new FarmLayout(farm.FieldCount, farm.ChargerCount, machines.Count);
            runtime.Tasks = new TaskCoordinator(farm.Id, runtime.Layout, store.GetFields(farm.Id), () => config.Current, logger);
            runtime.Zones = new ZoneManager(farm.Id, logger);
            runtime.Chargers = new ChargingStationManager(farm.Id, runtime.Layout, logger);

            foreach (MachineRegistration registration in machines)
            {
                MachineRuntime machine = new MachineRuntime(registration, runtime.Layout.ParkingPosition(registration.SlotIndex));
                machine.LoadStatistics(store.GetMachineStats(registration.Id));
                runtime.Machines[registration.Id] = machine;
            }

            farms[farm.Id] = runtime;
        }

        public void RemoveFarm(int farmId)
        {
            FarmRuntime removed;
            if (farms.TryRemove(farmId, out removed))
            {
                // Desconectar sus máquinas
                foreach (MachineRuntime machine in removed.GetMachines())
                {
                    machine.Disconnected = true;
                    if (machine.Connection != null)
                    {
                        machine.Connection.Close();
                    }
                }
                removed.WakeAllWaiters();
            }
        }

        public FarmRuntime Get(int farmId)
        {
            FarmRuntime farm;
            farms.TryGetValue(farmId, out farm);
            return farm;
        }

        public List<FarmRuntime> GetAll()
        {
            List<FarmRuntime> list = new List<FarmRuntime>(farms.Values);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>Busca una máquina en TODAS las explotaciones (después se comprueba a cuál pertenece).</summary>
        public MachineRuntime FindMachine(string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
            {
                return null;
            }
            foreach (FarmRuntime farm in farms.Values)
            {
                MachineRuntime machine;
                if (farm.Machines.TryGetValue(machineId, out machine))
                {
                    return machine;
                }
            }
            return null;
        }

        /// <summary>Guarda en disco el estado de los campos y las estadísticas de las máquinas.</summary>
        public void SaveAll()
        {
            List<AgroCore.Shared.DTOs.MachineStatistics> stats = new List<AgroCore.Shared.DTOs.MachineStatistics>();
            foreach (FarmRuntime farm in farms.Values)
            {
                store.SaveFields(farm.Id, farm.Tasks.GetFieldsSnapshot());
                foreach (MachineRuntime machine in farm.GetMachines())
                {
                    stats.Add(machine.GetStatistics());
                }
            }
            store.SaveMachineStats(stats);
        }
    }
}
