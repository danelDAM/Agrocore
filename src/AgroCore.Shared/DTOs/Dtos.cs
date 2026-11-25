using System;
using System.Collections.Generic;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.DTOs
{
    /// <summary>Bezeroari bidaltzen zaion erabiltzailea (pasahitzaren hash-a gabe).</summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public UserRole Role { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    /// <summary>Bezeroari bidaltzen zaion ustiategia.</summary>
    public class FarmDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int OwnerId { get; set; }
        public string OwnerName { get; set; }
        public int FieldCount { get; set; }
        public int ChargerCount { get; set; }
        public int MachineCount { get; set; }
        public int OnlineMachines { get; set; }
    }

    /// <summary>Makina batek erreserbatutako mapako gunea (talken aurkako sistema).</summary>
    public class ZoneReservation
    {
        public string ZoneId { get; set; } = "";
        public string MachineId { get; set; } = "";
    }

    /// <summary>Aginte-panelean erakusten diren laburpen-zenbakiak.</summary>
    public class FarmSummary
    {
        public int MachineCount { get; set; }
        public int OnlineMachines { get; set; }
        public int Working { get; set; }
        public int Moving { get; set; }
        public int Charging { get; set; }
        public int Waiting { get; set; }
        public int Available { get; set; }
        public int Returning { get; set; }
        public int Stopped { get; set; }
        public double AverageBattery { get; set; }
        public int FieldCount { get; set; }
        public int FieldsCultivated { get; set; }
        public int FieldsPending { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksActive { get; set; }
        public int TasksWaiting { get; set; }
        public int ChargersBusy { get; set; }
        public int ChargerQueue { get; set; }
        public int ConflictsAvoided { get; set; }
    }

    /// <summary>Makina baten estatistikak.</summary>
    public class MachineStatistics
    {
        public string MachineId { get; set; } = "";
        public string Name { get; set; } = "";
        public MachineType Type { get; set; }
        public double DistanceKm { get; set; }
        public double ActiveMinutes { get; set; }
        public double WorkingMinutes { get; set; }
        public double MovingMinutes { get; set; }
        public double WaitingMinutes { get; set; }
        public double ChargingMinutes { get; set; }
        public double BatteryConsumed { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksAborted { get; set; }
        public int ChargeCycles { get; set; }

        /// <summary>Denbora aktiboaren %, lanean edo lanera bidean igarotakoa.</summary>
        public double Efficiency { get; set; }
    }

    /// <summary>Ustiategi baten estatistikak.</summary>
    public class FarmStatistics
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public double TotalRuntimeMinutes { get; set; }
        public double DistanceKm { get; set; }
        public int FieldsSown { get; set; }
        public int FieldsIrrigated { get; set; }
        public int FieldsSprayed { get; set; }
        public int FieldsHarvested { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksCancelled { get; set; }
        public int TasksAborted { get; set; }
        public double WaitingMinutes { get; set; }
        public double ChargingMinutes { get; set; }
        public double BatteryConsumed { get; set; }
        public int ConflictsAvoided { get; set; }
        public int DeadlocksAvoided { get; set; }
        public int Emergencies { get; set; }
        public int LowBatteryEvents { get; set; }
        public int ChargeSessions { get; set; }
        public int AccessDenied { get; set; }
        public List<MachineStatistics> Machines { get; set; } = new List<MachineStatistics>();
    }

    /// <summary>PDF txostenaren datuak. Zerbitzariak baimendutako ustiategiko datuak soilik sartzen ditu.</summary>
    public class ReportData
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string GeneratedBy { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string FarmerName { get; set; } = "";
        public FarmSummary Summary { get; set; } = new FarmSummary();
        public FarmStatistics Statistics { get; set; } = new FarmStatistics();
        public List<Machine> Machines { get; set; } = new List<Machine>();
        public List<Field> Fields { get; set; } = new List<Field>();
        public List<FarmTask> Tasks { get; set; } = new List<FarmTask>();
        public List<LogEntry> Incidents { get; set; } = new List<LogEntry>();
    }
}
