using System;
using System.Collections.Generic;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    /// <summary>Administratzailea soilik: zein ustiategi ikusi aukeratu.</summary>
    public class SelectFarmRequest : NetMessage
    {
        public int FarmId { get; set; }

        public SelectFarmRequest()
        {
            Type = MessageTypes.SelectFarm;
        }
    }

    /// <summary>WPF -> zerbitzaria: telemetria jaso nahi duen UDP ataka.</summary>
    public class UdpSubscribeRequest : NetMessage
    {
        public int UdpPort { get; set; }

        public UdpSubscribeRequest()
        {
            Type = MessageTypes.UdpSubscribe;
        }
    }

    /// <summary>Zerbitzaria -> WPF (segundoro): ustiategi BATEN egoera osoa.</summary>
    public class FarmStateMessage : NetMessage
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public int FieldCount { get; set; }
        public int ChargerCount { get; set; }
        public bool EmergencyActive { get; set; }
        public int SimulationSpeed { get; set; } = 1;
        public FarmSummary Summary { get; set; } = new FarmSummary();
        public List<Machine> Machines { get; set; } = new List<Machine>();
        public List<Field> Fields { get; set; } = new List<Field>();
        public List<Charger> Chargers { get; set; } = new List<Charger>();
        public List<FarmTask> Tasks { get; set; } = new List<FarmTask>();
        public List<ZoneReservation> Zones { get; set; } = new List<ZoneReservation>();
        public List<Alert> Alerts { get; set; } = new List<Alert>();
        public List<CropDefinition> Crops { get; set; } = new List<CropDefinition>();

        public FarmStateMessage()
        {
            Type = MessageTypes.FarmState;
        }
    }

    /// <summary>Labore-plana: zerbitzariak berak sortzen ditu zeregin guztiak eta haien mendekotasunak.</summary>
    public class CropPlanRequest : NetMessage
    {
        public string FieldId { get; set; } = "";
        public CropType Crop { get; set; }
        public bool AutoReplant { get; set; } = true;

        public CropPlanRequest()
        {
            Type = MessageTypes.CreateCropPlan;
        }
    }

    public class CancelTaskRequest : NetMessage
    {
        public string TaskId { get; set; } = "";

        public CancelTaskRequest()
        {
            Type = MessageTypes.CancelTask;
        }
    }

    public class StatisticsRequest : NetMessage
    {
        public int FarmId { get; set; }

        public StatisticsRequest()
        {
            Type = MessageTypes.StatisticsRequest;
        }
    }

    public class StatisticsResponse : ResponseMessage
    {
        public FarmStatistics Statistics { get; set; }

        public StatisticsResponse()
        {
            Type = MessageTypes.StatisticsResponse;
        }
    }

    public class GlobalStatisticsRequest : NetMessage
    {
        public GlobalStatisticsRequest()
        {
            Type = MessageTypes.GlobalStatisticsRequest;
        }
    }

    public class GlobalStatisticsResponse : ResponseMessage
    {
        public List<FarmStatistics> Farms { get; set; } = new List<FarmStatistics>();

        public GlobalStatisticsResponse()
        {
            Type = MessageTypes.GlobalStatisticsResponse;
        }
    }

    public class ReportRequest : NetMessage
    {
        public int FarmId { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public ReportRequest()
        {
            Type = MessageTypes.ReportRequest;
        }
    }

    public class ReportResponse : ResponseMessage
    {
        public ReportData Report { get; set; }

        public ReportResponse()
        {
            Type = MessageTypes.ReportResponse;
        }
    }

    public class LogsRequest : NetMessage
    {
        public int FarmId { get; set; }
        public int Count { get; set; } = 200;

        public LogsRequest()
        {
            Type = MessageTypes.LogsRequest;
        }
    }

    public class LogsResponse : ResponseMessage
    {
        public List<LogEntry> Entries { get; set; } = new List<LogEntry>();

        public LogsResponse()
        {
            Type = MessageTypes.LogsResponse;
        }
    }
}
