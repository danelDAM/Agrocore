using System.Collections.Generic;
using System.Threading;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;

namespace AgroCore.Server.Collision
{
    /// <summary>
    /// Ustiategi baten TALKEN AURKAKO SISTEMA.
    ///
    /// Bidea guneetan (gelaxketan) banatuta dago. Gune batean sartu aurretik, makinak hemen
    /// erreserbatzen du. Beste makina batek okupatuta badu, haria Monitor.Wait()-ekin itxaroten du.
    /// Makina batek gune bat askatzen duenean, Monitor.PulseAll() deitzen da eta zain zeudenek
    /// berriro egiaztatzen dute pasa daitezkeen.
    ///
    ///   Seeder-01    -> B gunea eskatu -> libre     -> erreserbatuta -> jarraitu
    ///   Irrigator-01 -> B gunea eskatu -> okupatuta -> WAITING (Monitor.Wait)
    ///   Seeder-01    -> B gunea askatu -> Monitor.PulseAll -> Irrigator-01 sartzen da
    ///
    /// ELKARBLOKEOA (deadlock): A 1. gunean badago eta 2.a nahi badu, eta B 2.ean badago eta 1.a nahi badu,
    /// betiko egongo lirateke zain. Itxaron aurretik, itxaronaldien ziklo bat sortuko litzatekeen egiaztatzen da;
    /// hala bada, eskaera UKATU egiten da, eta makina hori bazterrera aldentzen da eta bidea uzten du.
    /// </summary>
    public class ZoneManager
    {
        private readonly object sync = new object();
        private readonly int farmId;
        private readonly EventLogger logger;

        /// <summary>Gunea -> hura erreserbatuta duen makina.</summary>
        private readonly Dictionary<string, string> holders = new Dictionary<string, string>();

        /// <summary>Makina -> itxaroten ari den gunea.</summary>
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

        /// <summary>Gune bat erreserbatzen du. Okupatuta badago, itxaron egiten du (Monitor.Wait) libre geratu arte.</summary>
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
                                machine.Name + " makinak " + zoneId + " erreserbatu du", farmId, machine.Id);
                            return ZoneOutcome.Granted;
                        }

                        // Itxaroteak itxaronaldien ziklo bat sortuko luke? Orduan ukatu egiten da.
                        if (WouldCreateDeadlock(machine.Id, zoneId))
                        {
                            deadlocksAvoided++;
                            logger.Log(EventKind.DeadlockAvoided, LogSeverity.Warning,
                                "Elkarblokeoa saihestuta: " + machine.Name + " makinak bidea uzten dio " + ShortName(holder) + " makinari " + zoneId + " gunean",
                                farmId, machine.Id);
                            return ZoneOutcome.DeniedDeadlock;
                        }

                        if (!conflictLogged)
                        {
                            conflictLogged = true;
                            conflictsAvoided++;
                            machine.SetNote("zain " + zoneId + " gunea (" + ShortName(holder) + " makinak okupatuta)");
                            logger.Log(EventKind.ZoneConflict, LogSeverity.Info,
                                machine.Name + " zain: " + zoneId + " gunea " + ShortName(holder) + " makinak okupatuta",
                                farmId, machine.Id);
                        }

                        waitingFor[machine.Id] = zoneId;
                        Monitor.Wait(sync, 1000); // PulseAll-ekin esnatzen da gune bat askatzean
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
        /// "Nahi dudan gunea X-k du, X Y-k duen gunearen zain dago..." katea jarraitzen du.
        /// Katea niregana itzultzen bada, ziklo bat dago: elkarblokeoa izango litzateke.
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
                    return false; // gunea libre dago: ez dago ziklorik
                }
                if (holder == requesterId)
                {
                    return true; // katea niregana itzultzen da
                }
                if (!visited.Add(holder))
                {
                    return false; // beste makinen arteko zikloa, ez nirekin
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
                        machine.Name + " makinak " + zoneId + " askatu du", farmId, machine.Id);
                    Monitor.PulseAll(sync); // gune honen zain zegoena esnatu
                }
            }
        }

        /// <summary>Makina baten gune guztiak askatzen ditu (deskonektatzean).</summary>
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
