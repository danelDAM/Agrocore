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
    /// Ustiategi baten karga-estazioak, ITXARON-ILARAREKIN.
    ///
    /// Makinak baino kargagailu gutxiago daude (adib. 11 makina eta 3 kargagailu). Hainbatek
    /// aldi berean kargatu behar badute:   3 -> CHARGING,  gainerakoak -> WAITING_FOR_CHARGER.
    ///
    /// - Ilara FIFO da (lehenengo iristen dena, lehenengo kargatzen da): horrela inor ez dago betiko zain.
    /// - Ilaran dagoen makina bakoitzak Monitor.Wait()-ekin itxaroten du.
    /// - Kargagailu bat libre geratzen denean: Monitor.PulseAll() eta zain daudenek berriro egiaztatzen dute
    ///   haien txanda den (ilarako lehenengoa bakarrik pasatzen da).
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

        /// <summary>Kargagailu bat eskatzen du. Blokeatuta geratzen da lortu arte (edo itxaronaldia bertan behera utzi arte).</summary>
        public ChargerOutcome Acquire(MachineRuntime machine, out Charger assigned)
        {
            assigned = null;
            lock (sync)
            {
                // Bazuen bat jada? (eskaera errepikatzen badu ere)
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
                                machine.Name + " kargagailura konektatuta: " + free.Id, farmId, machine.Id);
                            return ChargerOutcome.Granted;
                        }

                        if (!waitLogged)
                        {
                            waitLogged = true;
                            int position = PositionInQueue(machine.Id);
                            machine.SetNote("karga-ilaran (" + position + ". postua)");
                            logger.Log(EventKind.WaitingForCharger, LogSeverity.Warning,
                                machine.Name + " kargagailuaren zain (ilarako " + position + ". postua)", farmId, machine.Id);
                        }
                        Monitor.Wait(sync, 1000);
                    }
                }
                finally
                {
                    queue.Remove(machine.Id);
                    machine.SetNote(null);
                    // Ilaratik ateratzean, hurrengoa lehenengoa izan daiteke: abisatu.
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
                            machine.Name + " makinak " + charger.Id + " askatu du (100%)", farmId, machine.Id);
                    }
                }
                Monitor.PulseAll(sync); // kargagailu bat libre: ilarako hurrengoak har dezala
            }
        }

        /// <summary>Makina bat deskonektatzean, bere kargagailua askatzen da karga osoa erregistratu gabe.</summary>
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
