using System.Collections.Generic;
using System.Threading;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Charging
{
    /// <summary>
    /// Estaciones de carga de una explotación con COLA DE ESPERA.
    ///
    /// Hay menos cargadores que máquinas (p. ej. 11 máquinas y 3 cargadores). Si varias
    /// necesitan cargar a la vez:   3 -> CHARGING,  el resto -> WAITING_FOR_CHARGER.
    ///
    /// - La cola es FIFO (la primera que llega, la primera que carga): así nadie espera para siempre.
    /// - Cada máquina en cola espera con Monitor.Wait().
    /// - Cuando un cargador queda libre: Monitor.PulseAll() y las que esperan vuelven a comprobar
    ///   si ya les toca (solo pasa la primera de la cola).
    /// </summary>
    public class ChargingStationManager
    {
        private readonly object sync = new object();
        private readonly int farmId;
        private readonly EventLogger logger;
        private readonly List<Charger> chargers = new List<Charger>();
        private readonly LinkedList<string> queue = new LinkedList<string>();

        public ChargingStationManager(int farmId, FarmLayout layout, EventLogger logger)
        {
            this.farmId = farmId;
            this.logger = logger;
            for (int i = 0; i < layout.ChargerCount; i++)
            {
                Charger charger = new Charger();
                charger.Id = "CHARGER-" + (i + 1).ToString("00");
                charger.FarmId = farmId;
                charger.Index = i;
                charger.Position = layout.ChargerPosition(i);
                chargers.Add(charger);
            }
        }

        /// <summary>Pide un cargador. Bloquea hasta conseguirlo (o hasta que se cancele la espera).</summary>
        public ChargerOutcome Acquire(MachineRuntime machine, out Charger assigned)
        {
            assigned = null;
            lock (sync)
            {
                // ¿Ya tenía uno? (por si repite la petición)
                foreach (Charger charger in chargers)
                {
                    if (charger.OccupiedBy == machine.Id)
                    {
                        assigned = charger.Clone();
                        return ChargerOutcome.Granted;
                    }
                }

                queue.AddLast(machine.Id);
                bool waitLogged = false;
                try
                {
                    while (true)
                    {
                        if (machine.ShouldAbortWait(false))
                        {
                            return ChargerOutcome.Cancelled;
                        }

                        Charger free = FindFreeCharger();
                        bool myTurn = queue.First.Value == machine.Id;
                        if (myTurn && free != null)
                        {
                            free.OccupiedBy = machine.Id;
                            assigned = free.Clone();
                            machine.CountChargeCycle();
                            logger.Log(EventKind.ChargingStarted, LogSeverity.Info,
                                machine.Name + " conectada a " + free.Id, farmId, machine.Id);
                            return ChargerOutcome.Granted;
                        }

                        if (!waitLogged)
                        {
                            waitLogged = true;
                            int position = PositionInQueue(machine.Id);
                            machine.SetNote("en cola de carga (puesto " + position + ")");
                            logger.Log(EventKind.WaitingForCharger, LogSeverity.Warning,
                                machine.Name + " esperando cargador (puesto " + position + " en la cola)", farmId, machine.Id);
                        }
                        Monitor.Wait(sync, 1000);
                    }
                }
                finally
                {
                    queue.Remove(machine.Id);
                    machine.SetNote(null);
                    // Al salir de la cola, la siguiente puede pasar a ser la primera: avisar.
                    Monitor.PulseAll(sync);
                }
            }
        }

        public void Release(MachineRuntime machine, string chargerId)
        {
            lock (sync)
            {
                foreach (Charger charger in chargers)
                {
                    if (charger.OccupiedBy == machine.Id && (chargerId == null || charger.Id == chargerId))
                    {
                        charger.OccupiedBy = null;
                        logger.Log(EventKind.ChargingCompleted, LogSeverity.Success,
                            machine.Name + " liberó " + charger.Id + " (100%)", farmId, machine.Id);
                    }
                }
                Monitor.PulseAll(sync); // un cargador libre: que la siguiente de la cola lo coja
            }
        }

        /// <summary>Al desconectarse una máquina, se libera su cargador sin registrar carga completa.</summary>
        public void ReleaseAll(MachineRuntime machine)
        {
            lock (sync)
            {
                foreach (Charger charger in chargers)
                {
                    if (charger.OccupiedBy == machine.Id)
                    {
                        charger.OccupiedBy = null;
                    }
                }
                Monitor.PulseAll(sync);
            }
        }

        public void WakeAll()
        {
            lock (sync)
            {
                Monitor.PulseAll(sync);
            }
        }

        public List<Charger> GetSnapshot()
        {
            lock (sync)
            {
                List<Charger> copy = new List<Charger>();
                foreach (Charger charger in chargers)
                {
                    Charger c = charger.Clone();
                    c.QueueLength = queue.Count;
                    copy.Add(c);
                }
                return copy;
            }
        }

        public int QueueLength
        {
            get { lock (sync) { return queue.Count; } }
        }

        private Charger FindFreeCharger()
        {
            foreach (Charger charger in chargers)
            {
                if (charger.OccupiedBy == null)
                {
                    return charger;
                }
            }
            return null;
        }

        private int PositionInQueue(string machineId)
        {
            int position = 1;
            foreach (string id in queue)
            {
                if (id == machineId)
                {
                    return position;
                }
                position++;
            }
            return position;
        }
    }
}
