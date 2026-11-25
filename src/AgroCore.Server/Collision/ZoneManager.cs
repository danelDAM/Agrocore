using System.Collections.Generic;
using System.Threading;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;

namespace AgroCore.Server.Collision
{
    /// <summary>
    /// SISTEMA ANTICOLISIÓN de una explotación.
    ///
    /// El camino está dividido en zonas (celdas). Antes de entrar en una zona, la máquina la
    /// reserva aquí. Si está ocupada por otra máquina, el hilo espera con Monitor.Wait().
    /// Cuando una máquina libera una zona se llama a Monitor.PulseAll() y las que esperaban
    /// vuelven a comprobar si ya pueden pasar.
    ///
    ///   Seeder-01    -> pide Zona B -> libre   -> reservada -> continúa
    ///   Irrigator-01 -> pide Zona B -> ocupada -> WAITING (Monitor.Wait)
    ///   Seeder-01    -> libera Zona B -> Monitor.PulseAll -> Irrigator-01 entra
    ///
    /// INTERBLOQUEO (deadlock): si A está en la zona 1 y quiere la 2, y B está en la 2 y quiere la 1,
    /// esperarían para siempre. Antes de esperar se comprueba si se formaría un ciclo de esperas;
    /// si es así se DENIEGA la petición y esa máquina se aparta al arcén y cede el paso.
    /// </summary>
    public class ZoneManager
    {
        private readonly object sync = new object();
        private readonly int farmId;
        private readonly EventLogger logger;

        /// <summary>Zona -> máquina que la tiene reservada.</summary>
        private readonly Dictionary<string, string> holders = new Dictionary<string, string>();

        /// <summary>Máquina -> zona que está esperando.</summary>
        private readonly Dictionary<string, string> waitingFor = new Dictionary<string, string>();

        private int conflictsAvoided;
        private int deadlocksAvoided;

        public ZoneManager(int farmId, EventLogger logger)
        {
            this.farmId = farmId;
            this.logger = logger;
        }

        public int ConflictsAvoided
        {
            get { lock (sync) { return conflictsAvoided; } }
        }

        public int DeadlocksAvoided
        {
            get { lock (sync) { return deadlocksAvoided; } }
        }

        /// <summary>Reserva una zona. Si está ocupada, espera (Monitor.Wait) hasta que quede libre.</summary>
        public ZoneOutcome Acquire(MachineRuntime machine, string zoneId)
        {
            lock (sync)
            {
                bool conflictLogged = false;
                try
                {
                    while (true)
                    {
                        if (machine.ShouldAbortWait(false))
                        {
                            return ZoneOutcome.Cancelled;
                        }

                        string holder;
                        bool occupied = holders.TryGetValue(zoneId, out holder);
                        if (!occupied || holder == machine.Id)
                        {
                            holders[zoneId] = machine.Id;
                            logger.Log(EventKind.ZoneReserved, LogSeverity.Debug,
                                machine.Name + " reserva " + zoneId, farmId, machine.Id);
                            return ZoneOutcome.Granted;
                        }

                        // ¿Esperar crearía un ciclo de esperas? Entonces se deniega.
                        if (WouldCreateDeadlock(machine.Id, zoneId))
                        {
                            deadlocksAvoided++;
                            logger.Log(EventKind.DeadlockAvoided, LogSeverity.Warning,
                                "Interbloqueo evitado: " + machine.Name + " cede el paso a " + ShortName(holder) + " en " + zoneId,
                                farmId, machine.Id);
                            return ZoneOutcome.DeniedDeadlock;
                        }

                        if (!conflictLogged)
                        {
                            conflictLogged = true;
                            conflictsAvoided++;
                            machine.SetNote("esperando zona " + zoneId + " (ocupada por " + ShortName(holder) + ")");
                            logger.Log(EventKind.ZoneConflict, LogSeverity.Info,
                                machine.Name + " espera: zona " + zoneId + " ocupada por " + ShortName(holder),
                                farmId, machine.Id);
                        }

                        waitingFor[machine.Id] = zoneId;
                        Monitor.Wait(sync, 1000); // se despierta con PulseAll al liberarse una zona
                    }
                }
                finally
                {
                    waitingFor.Remove(machine.Id);
                    machine.SetNote(null);
                }
            }
        }

        /// <summary>
        /// Sigue la cadena "la zona que quiero la tiene X, X espera la zona que tiene Y..."
        /// Si la cadena vuelve a mí, hay un ciclo: sería un interbloqueo.
        /// </summary>
        private bool WouldCreateDeadlock(string requesterId, string zoneId)
        {
            HashSet<string> visited = new HashSet<string>();
            string zone = zoneId;
            while (zone != null)
            {
                string holder;
                if (!holders.TryGetValue(zone, out holder))
                {
                    return false; // la zona está libre: no hay ciclo
                }
                if (holder == requesterId)
                {
                    return true; // la cadena vuelve a mí
                }
                if (!visited.Add(holder))
                {
                    return false; // ciclo entre otras máquinas, no conmigo
                }
                string next;
                zone = waitingFor.TryGetValue(holder, out next) ? next : null;
            }
            return false;
        }

        public void Release(MachineRuntime machine, string zoneId)
        {
            lock (sync)
            {
                string holder;
                if (holders.TryGetValue(zoneId, out holder) && holder == machine.Id)
                {
                    holders.Remove(zoneId);
                    logger.Log(EventKind.ZoneReleased, LogSeverity.Debug,
                        machine.Name + " libera " + zoneId, farmId, machine.Id);
                    Monitor.PulseAll(sync); // despertar a quien esperaba esta zona
                }
            }
        }

        /// <summary>Libera todas las zonas de una máquina (al desconectarse).</summary>
        public void ReleaseAll(MachineRuntime machine)
        {
            lock (sync)
            {
                List<string> mine = new List<string>();
                foreach (KeyValuePair<string, string> pair in holders)
                {
                    if (pair.Value == machine.Id)
                    {
                        mine.Add(pair.Key);
                    }
                }
                foreach (string zone in mine)
                {
                    holders.Remove(zone);
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

        public List<ZoneReservation> GetReservations()
        {
            lock (sync)
            {
                List<ZoneReservation> list = new List<ZoneReservation>();
                foreach (KeyValuePair<string, string> pair in holders)
                {
                    ZoneReservation reservation = new ZoneReservation();
                    reservation.ZoneId = pair.Key;
                    reservation.MachineId = pair.Value;
                    list.Add(reservation);
                }
                return list;
            }
        }

        /// <summary>"F1-Seeder-01" -> "Seeder-01".</summary>
        private static string ShortName(string machineId)
        {
            int dash = machineId.IndexOf('-');
            return dash >= 0 ? machineId.Substring(dash + 1) : machineId;
        }
    }
}
