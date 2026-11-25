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
    /// Todo lo que está "vivo" de UNA explotación: su coordinador de tareas, su sistema de zonas,
    /// sus cargadores y sus máquinas. Cada explotación tiene los suyos propios, así los datos
    /// de un agricultor nunca se mezclan con los de otro.
    /// </summary>
    public class FarmRuntime
    {
        public Farm Farm { get; set; }
        public string OwnerName { get; set; } = "";
        public FarmLayout Layout { get; set; }
        public TaskCoordinator Tasks { get; set; }
        public ZoneManager Zones { get; set; }
        public ChargingStationManager Chargers { get; set; }

        /// <summary>Máquinas de la explotación (clave = id de máquina).</summary>
        public ConcurrentDictionary<string, MachineRuntime> Machines { get; } = new ConcurrentDictionary<string, MachineRuntime>();

        /// <summary>Parada de emergencia activa (hasta RESUME_ALL).</summary>
        public volatile bool EmergencyActive;

        public int Id
        {
            get { return Farm.Id; }
        }

        /// <summary>Máquinas ordenadas por su plaza en la base.</summary>
        public List<MachineRuntime> GetMachines()
        {
            List<MachineRuntime> list = new List<MachineRuntime>(Machines.Values);
            list.Sort((a, b) => a.Registration.SlotIndex.CompareTo(b.Registration.SlotIndex));
            return list;
        }

        /// <summary>
        /// Despierta a TODOS los hilos que esperan en esta explotación (tareas, zonas y cargadores).
        /// Se usa tras STOP, EMERGENCY_STOP o RETURN_TO_BASE para que comprueben que deben dejar de esperar.
        /// </summary>
        public void WakeAllWaiters()
        {
            Tasks.WakeAll();
            Zones.WakeAll();
            Chargers.WakeAll();
        }
    }
}
