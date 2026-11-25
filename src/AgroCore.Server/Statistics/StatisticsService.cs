using System;
using System.Collections.Generic;
using AgroCore.Server.Logging;
using AgroCore.Server.Machines;
using AgroCore.Server.Synchronization;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Server.Statistics
{
    /// <summary>
    /// Calcula las estadísticas de una explotación.
    ///  - Contadores de eventos (campos sembrados, tareas completadas, conflictos, emergencias...):
    ///    se obtienen CONTANDO entradas del log en el periodo pedido.
    ///  - Datos de cada máquina (distancia, tiempo activo, carga...): contadores acumulados
    ///    que el servidor calcula a partir de la telemetría.
    /// </summary>
    public class StatisticsService
    {
        private readonly EventLogger logger;
        private readonly FarmManager farms;

        public StatisticsService(EventLogger logger, FarmManager farms)
        {
            this.logger = logger;
            this.farms = farms;
        }

        public FarmStatistics Build(FarmRuntime farm, DateTime from, DateTime to)
        {
            FarmStatistics stats = new FarmStatistics();
            stats.FarmId = farm.Id;
            stats.FarmName = farm.Farm.Name;
            stats.From = from;
            stats.To = to;

            // 1. Contar eventos del log
            foreach (LogEntry entry in logger.GetEntries(farm.Id, from, to))
            {
                switch (entry.Kind)
                {
                    case EventKind.TaskCompleted:
                        stats.TasksCompleted++;
                        if (entry.TaskType == TaskType.Sow) stats.FieldsSown++;
                        if (entry.TaskType == TaskType.Irrigate) stats.FieldsIrrigated++;
                        if (entry.TaskType == TaskType.Spray) stats.FieldsSprayed++;
                        if (entry.TaskType == TaskType.Harvest) stats.FieldsHarvested++;
                        break;
                    case EventKind.TaskCancelled:
                        stats.TasksCancelled++;
                        break;
                    case EventKind.TaskAborted:
                        stats.TasksAborted++;
                        break;
                    case EventKind.ZoneConflict:
                        stats.ConflictsAvoided++;
                        break;
                    case EventKind.DeadlockAvoided:
                        stats.DeadlocksAvoided++;
                        stats.ConflictsAvoided++;
                        break;
                    case EventKind.EmergencyStop:
                        stats.Emergencies++;
                        break;
                    case EventKind.LowBattery:
                        stats.LowBatteryEvents++;
                        break;
                    case EventKind.ChargingStarted:
                        stats.ChargeSessions++;
                        break;
                    case EventKind.AccessDenied:
                        stats.AccessDenied++;
                        break;
                }
            }

            // 2. Sumar los datos de cada máquina
            foreach (MachineRuntime machine in farm.GetMachines())
            {
                MachineStatistics m = machine.GetStatistics();
                stats.Machines.Add(m);
                stats.TotalRuntimeMinutes += m.ActiveMinutes;
                stats.DistanceKm += m.DistanceKm;
                stats.WaitingMinutes += m.WaitingMinutes;
                stats.ChargingMinutes += m.ChargingMinutes;
                stats.BatteryConsumed += m.BatteryConsumed;
            }
            return stats;
        }

        /// <summary>Estadísticas de todas las explotaciones (solo administrador).</summary>
        public List<FarmStatistics> BuildGlobal(DateTime from, DateTime to)
        {
            List<FarmStatistics> list = new List<FarmStatistics>();
            foreach (FarmRuntime farm in farms.GetAll())
            {
                list.Add(Build(farm, from, to));
            }
            return list;
        }
    }
}
