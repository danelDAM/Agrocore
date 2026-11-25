using System.Collections.Generic;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    /// <summary>Makina -> zerbitzaria: flota-gakoarekin autentifikatzen da eta bere gaitasunak bidaltzen ditu.</summary>
    public class MachineAuthRequest : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string FleetKey { get; set; } = "";
        public MachineType MachineType { get; set; }
        public MachineProfile Profile { get; set; } = new MachineProfile();
        public double InitialBattery { get; set; } = 100;

        public MachineAuthRequest()
        {
            Type = MessageTypes.MachineAuth;
        }
    }

    /// <summary>Zerbitzaria -> makina: bere ustiategian lan egiteko behar dituen datuak.</summary>
    public class MachineAuthResponse : ResponseMessage
    {
        public string MachineId { get; set; }
        public string MachineName { get; set; }
        public int FarmId { get; set; }
        public string SessionToken { get; set; }
        public int FieldCount { get; set; }
        public int ChargerCount { get; set; }
        public int SlotIndex { get; set; }
        public int SlotCount { get; set; }

        /// <summary>Dronearen zenbakia ustiategiaren barruan (bere hegaldi-altuera zehazten du).</summary>
        public int DroneIndex { get; set; }

        public int UdpPort { get; set; }
        public int TelemetryIntervalMs { get; set; }
        public BatteryConfig Battery { get; set; } = new BatteryConfig();

        /// <summary>Ustiategiaren uneko simulazio-abiadura (1 = denbora erreala, 10 = hamar aldiz azkarrago).</summary>
        public int SimulationSpeed { get; set; } = 1;

        public MachineAuthResponse()
        {
            Type = MessageTypes.MachineAuthResponse;
        }
    }

    /// <summary>Flota-abiarazlea -> zerbitzaria: ustiategi bateko makinen zerrenda eskatzen du.</summary>
    public class FleetQueryRequest : NetMessage
    {
        public int FarmId { get; set; }
        public string FleetKey { get; set; } = "";

        public FleetQueryRequest()
        {
            Type = MessageTypes.FleetQuery;
        }
    }

    public class FleetQueryResponse : ResponseMessage
    {
        public List<MachineRegistration> Machines { get; set; } = new List<MachineRegistration>();

        public FleetQueryResponse()
        {
            Type = MessageTypes.FleetResponse;
        }
    }

    /// <summary>
    /// Makina -> zerbitzaria: "emadazu lana". Makina honi erantzuten dion zerbitzariaren haria
    /// Monitor.Wait()-en geratzen da harentzako zeregin egoki bat egon arte.
    /// </summary>
    public class TaskRequest : NetMessage
    {
        public string MachineId { get; set; } = "";

        /// <summary>Itxaronaldiaren gehieneko milisegundoak (0 = behar adina itxaron).</summary>
        public int MaxWaitMs { get; set; }

        public TaskRequest()
        {
            Type = MessageTypes.TaskRequest;
        }
    }

    /// <summary>Zerbitzaria -> makina: TASK_REQUEST-i erantzuna.</summary>
    public class TaskAssignment : ResponseMessage
    {
        public AssignmentOutcome Outcome { get; set; }
        public string MachineId { get; set; }
        public string TaskId { get; set; }
        public string FieldId { get; set; }
        public int FieldIndex { get; set; }
        public TaskType TaskType { get; set; }
        public CropType Crop { get; set; }

        /// <summary>Dagoeneko egindako aurrerapena (beste makina batek erdizka utzi bazuen).</summary>
        public double Progress { get; set; }

        public TaskAssignment()
        {
            Type = MessageTypes.TaskAssignment;
        }
    }

    public class TaskStarted : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string FieldId { get; set; } = "";

        public TaskStarted()
        {
            Type = MessageTypes.TaskStarted;
        }
    }

    public class TaskCompleted : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string FieldId { get; set; } = "";

        public TaskCompleted()
        {
            Type = MessageTypes.TaskCompleted;
        }
    }

    /// <summary>Makinak zeregin bat erdizka uzten du (bateria baxua, itzultzeko agindua...).</summary>
    public class TaskAborted : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string FieldId { get; set; } = "";
        public double Progress { get; set; }
        public string Reason { get; set; } = "";

        public TaskAborted()
        {
            Type = MessageTypes.TaskAborted;
        }
    }

    /// <summary>Zerbitzaria -> makina: operadoreak egiten ari zen zeregina bertan behera utzi du.</summary>
    public class TaskCancelledNotice : NetMessage
    {
        public string TaskId { get; set; } = "";
        public string Reason { get; set; } = "";

        public TaskCancelledNotice()
        {
            Type = MessageTypes.TaskCancelled;
        }
    }

    /// <summary>Makina -> zerbitzaria: bideko gune batean sartu nahi du.</summary>
    public class ZoneRequest : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string ZoneId { get; set; } = "";

        public ZoneRequest()
        {
            Type = MessageTypes.ZoneRequest;
        }
    }

    public class ZoneResponse : ResponseMessage
    {
        public string ZoneId { get; set; } = "";
        public ZoneOutcome Outcome { get; set; }

        public ZoneResponse()
        {
            Type = MessageTypes.ZoneResponse;
        }
    }

    /// <summary>Makina -> zerbitzaria: gunetik irten da dagoeneko.</summary>
    public class ZoneRelease : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string ZoneId { get; set; } = "";

        public ZoneRelease()
        {
            Type = MessageTypes.ZoneRelease;
        }
    }

    /// <summary>Makina -> zerbitzaria: kargagailu bat behar du (ilaran itxaroten du libre bat ere ez badago).</summary>
    public class ChargerRequest : NetMessage
    {
        public string MachineId { get; set; } = "";

        public ChargerRequest()
        {
            Type = MessageTypes.ChargerRequest;
        }
    }

    public class ChargerResponse : ResponseMessage
    {
        public ChargerOutcome Outcome { get; set; }
        public string ChargerId { get; set; }
        public int ChargerIndex { get; set; }

        public ChargerResponse()
        {
            Type = MessageTypes.ChargerResponse;
        }
    }

    public class ChargerRelease : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string ChargerId { get; set; } = "";

        public ChargerRelease()
        {
            Type = MessageTypes.ChargerRelease;
        }
    }

    /// <summary>Makina -> zerbitzaria: zerbait garrantzitsua gertatu da (bateria baxua, basera itzultzen da...).</summary>
    public class MachineEventMessage : NetMessage
    {
        public string MachineId { get; set; } = "";
        public EventKind Kind { get; set; }
        public LogSeverity Severity { get; set; } = LogSeverity.Info;
        public string Text { get; set; } = "";

        public MachineEventMessage()
        {
            Type = MessageTypes.MachineEvent;
        }
    }
}
