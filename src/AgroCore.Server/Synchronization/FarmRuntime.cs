using System.Collections.Concurrent;
using System.Collections.Generic;
using AgroCore.Server.Charging;
using AgroCore.Server.Collision;
using AgroCore.Server.Machines;
using AgroCore.Server.Tasks;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Synchronization
{
    /// <summary>
    /// Ustiategi BATEN "bizirik" dagoen guztia: bere zeregin-koordinatzailea, bere gune-sistema,
    /// bere kargagailuak eta bere makinak. Ustiategi bakoitzak bereak ditu; horrela, nekazari baten
    /// datuak ez dira inoiz beste batenekin nahasten.
    /// </summary>
    public class FarmRuntime
    {
        public Farm Farm { get; set; }
        public string OwnerName { get; set; } = "";
        public FarmLayout Layout { get; set; }
        public TaskCoordinator Tasks { get; set; }
        public ZoneManager Zones { get; set; }
        public ChargingStationManager Chargers { get; set; }

        /// <summary>Ustiategiko makinak (gakoa = makinaren id-a).</summary>
        public ConcurrentDictionary<string, MachineRuntime> Machines { get; } = new ConcurrentDictionary<string, MachineRuntime>();

        /// <summary>Larrialdiko geldialdia aktibo (RESUME_ALL arte).</summary>
        public volatile bool EmergencyActive;

        /// <summary>Simulazioaren abiadura: 1 = denbora erreala, 10 = hamar aldiz azkarrago (Kontrol Zentroko botoiak).</summary>
        public volatile int SimulationSpeed = 1;

        public int Id
        {
            get { return Farm.Id; }
        }

        /// <summary>Makinak, basean duten aparkalekuaren arabera ordenatuta.</summary>
        public List<MachineRuntime> GetMachines()
        {
            List<MachineRuntime> list = new List<MachineRuntime>(Machines.Values);
            list.Sort((a, b) => a.Registration.SlotIndex.CompareTo(b.Registration.SlotIndex));
            return list;
        }

        /// <summary>
        /// Ustiategi honetan zain dauden hari GUZTIAK esnatzen ditu (zereginak, guneak eta kargagailuak).
        /// STOP, EMERGENCY_STOP edo RETURN_TO_BASE ondoren erabiltzen da, itxaroteari utzi behar diotela egiazta dezaten.
        /// </summary>
        public void WakeAllWaiters()
        {
            Tasks.WakeAll();
            Zones.WakeAll();
            Chargers.WakeAll();
        }
    }
}
