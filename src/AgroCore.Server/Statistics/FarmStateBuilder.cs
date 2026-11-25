using System;
using System.Collections.Generic;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Messages;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Statistics
{
    /// <summary>
    /// Ustiategi baten FARM_STATE mezua eraikitzen du: makinak, soroak, zereginak, kargagailuak,
    /// erreserbatutako guneak, laburpena eta alertak. Ustiategi horren datuak SOILIK ditu.
    /// </summary>
    public class FarmStateBuilder
    {
        private readonly EventLogger logger;
        private readonly Func<SystemConfig> getConfig;

        public FarmStateBuilder(EventLogger logger, Func<SystemConfig> getConfig)
        {
            this.logger = logger;
            this.getConfig = getConfig;
        }

        public FarmStateMessage Build(FarmRuntime farm)
        {
            SystemConfig config = getConfig();
            FarmStateMessage state = new FarmStateMessage();
            state.FarmId = farm.Id;
            state.FarmName = farm.Farm.Name;
            state.OwnerName = farm.OwnerName;
            state.FieldCount = farm.Layout.FieldCount;
            state.ChargerCount = farm.Layout.ChargerCount;
            state.EmergencyActive = farm.EmergencyActive;
            state.Crops = config.Crops;

            foreach (MachineRuntime machine in farm.GetMachines())
            {
                state.Machines.Add(machine.ToDto());
            }
            state.Fields = farm.Tasks.GetFieldsSnapshot();
            state.Tasks = farm.Tasks.GetTasksSnapshot(30);
            state.Chargers = farm.Chargers.GetSnapshot();
            state.Zones = farm.Zones.GetReservations();

            // Martxan dauden zereginen aurrerapena makinaren telemetriatik dator
            foreach (FarmTask task in state.Tasks)
            {
                if (task.Status == AgroTaskStatus.Running && task.AssignedMachineId != null)
                {
                    foreach (Machine machine in state.Machines)
                    {
                        if (machine.Id == task.AssignedMachineId)
                        {
                            task.Progress = Math.Max(task.Progress, machine.TaskProgress);
                        }
                    }
                }
            }

            state.Summary = BuildSummary(farm, state);
            state.Alerts = BuildAlerts(farm, state, config);
            return state;
        }

        private FarmSummary BuildSummary(FarmRuntime farm, FarmStateMessage state)
        {
            FarmSummary s = new FarmSummary();
            s.MachineCount = state.Machines.Count;
            double batterySum = 0;

            foreach (Machine m in state.Machines)
            {
                if (!m.Online)
                {
                    continue;
                }
                s.OnlineMachines++;
                batterySum += m.Battery;
                switch (m.Status)
                {
                    case MachineStatus.Working: s.Working++; break;
                    case MachineStatus.MovingToField: s.Moving++; break;
                    case MachineStatus.Charging: s.Charging++; break;
                    case MachineStatus.ReturningToBase: s.Returning++; break;
                    case MachineStatus.WaitingForTask:
                    case MachineStatus.WaitingForZone:
                    case MachineStatus.WaitingForCharger:
                    case MachineStatus.Yielding:
                        s.Waiting++;
                        break;
                    case MachineStatus.Stopped:
                    case MachineStatus.Emergency:
                    case MachineStatus.Parked:
                        s.Stopped++;
                        break;
                }
                if (m.Status == MachineStatus.Idle || m.Status == MachineStatus.WaitingForTask)
                {
                    s.Available++;
                }
            }
            s.AverageBattery = s.OnlineMachines > 0 ? batterySum / s.OnlineMachines : 0;

            s.FieldCount = state.Fields.Count;
            foreach (Field f in state.Fields)
            {
                if (f.Status == FieldStatus.Growing || f.Status == FieldStatus.ReadyToHarvest || f.Status == FieldStatus.Harvesting)
                {
                    s.FieldsCultivated++;
                }
                else
                {
                    s.FieldsPending++;
                }
            }

            foreach (FarmTask t in state.Tasks)
            {
                if (t.Status == AgroTaskStatus.Running || t.Status == AgroTaskStatus.Assigned) s.TasksActive++;
                if (t.Status == AgroTaskStatus.Waiting || t.Status == AgroTaskStatus.Pending) s.TasksWaiting++;
            }
            foreach (LogEntry e in logger.GetEntries(farm.Id, DateTime.Today, DateTime.Now))
            {
                if (e.Kind == EventKind.TaskCompleted) s.TasksCompleted++;
            }

            foreach (Charger c in state.Chargers)
            {
                if (c.OccupiedBy != null) s.ChargersBusy++;
                s.ChargerQueue = c.QueueLength;
            }
            s.ConflictsAvoided = farm.Zones.ConflictsAvoided;
            return s;
        }

        /// <summary>Alertak: uneko egoerak (bateria baxua, itxaronaldiak...) + azken gertaera garrantzitsuak.</summary>
        private List<Alert> BuildAlerts(FarmRuntime farm, FarmStateMessage state, SystemConfig config)
        {
            List<Alert> alerts = new List<Alert>();

            if (farm.EmergencyActive)
            {
                alerts.Add(NewAlert(AlertLevel.Error, "⛔ LARRIALDIKO GELDIALDIA AKTIBO"));
            }

            foreach (Machine m in state.Machines)
            {
                if (!m.Online)
                {
                    continue;
                }
                if (m.Battery <= config.Battery.LowBatteryThreshold && m.Status != MachineStatus.Charging)
                {
                    alerts.Add(NewAlert(AlertLevel.Warning, "⚠ " + m.Name + " bateria baxua (" + m.Battery.ToString("0") + "%)"));
                }
                if (m.Status == MachineStatus.WaitingForTask && m.Note != null && m.Note.StartsWith("zain FIELD"))
                {
                    alerts.Add(NewAlert(AlertLevel.Info, "⏳ " + m.Name + " " + m.Note));
                }
                if (m.Status == MachineStatus.WaitingForCharger)
                {
                    alerts.Add(NewAlert(AlertLevel.Warning, "⚠ " + m.Name + " kargagailuaren zain"));
                }
                if (m.Status == MachineStatus.WaitingForZone || m.Status == MachineStatus.Yielding)
                {
                    alerts.Add(NewAlert(AlertLevel.Info, "🚧 " + m.Name + " " + (m.Note ?? "gune baten zain")));
                }
                if (m.Status == MachineStatus.Stopped)
                {
                    alerts.Add(NewAlert(AlertLevel.Warning, "⏸ " + m.Name + " geldituta"));
                }
            }

            foreach (Charger c in state.Chargers)
            {
                if (c.OccupiedBy != null)
                {
                    alerts.Add(NewAlert(AlertLevel.Info, "🔌 " + c.Id + " okupatuta: " + ShortName(c.OccupiedBy)));
                }
            }

            // Gertaeren erregistroko azken gertaera garrantzitsuak
            foreach (LogEntry e in logger.GetRecent(farm.Id, 40, false))
            {
                if (alerts.Count >= 40)
                {
                    break;
                }
                if (e.Severity == LogSeverity.Success && e.Kind == EventKind.FieldStateChanged)
                {
                    alerts.Add(NewAlert(AlertLevel.Success, "✓ " + e.Message, e.Timestamp));
                }
                else if (e.Severity == LogSeverity.Warning && e.Kind != EventKind.WaitingForCharger)
                {
                    alerts.Add(NewAlert(AlertLevel.Warning, "⚠ " + e.Message, e.Timestamp));
                }
                else if (e.Severity == LogSeverity.Error || e.Severity == LogSeverity.Security)
                {
                    alerts.Add(NewAlert(AlertLevel.Error, "⛔ " + e.Message, e.Timestamp));
                }
            }
            return alerts;
        }

        private static Alert NewAlert(AlertLevel level, string text)
        {
            return NewAlert(level, text, DateTime.Now);
        }

        private static Alert NewAlert(AlertLevel level, string text, DateTime time)
        {
            Alert alert = new Alert();
            alert.Level = level;
            alert.Text = text;
            alert.Timestamp = time;
            return alert;
        }

        private static string ShortName(string machineId)
        {
            int dash = machineId.IndexOf('-');
            return dash >= 0 ? machineId.Substring(dash + 1) : machineId;
        }
    }
}
