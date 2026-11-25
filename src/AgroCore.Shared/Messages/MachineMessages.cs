using System.Collections.Generic;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    /// <summary>Máquina -> servidor: se autentica con la clave de flota y envía sus capacidades.</summary>
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

    /// <summary>Servidor -> máquina: datos que necesita para trabajar en su explotación.</summary>
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

        /// <summary>Número de dron dentro de la explotación (define su altura de vuelo).</summary>
        public int DroneIndex { get; set; }

        public int UdpPort { get; set; }
        public int TelemetryIntervalMs { get; set; }
        public BatteryConfig Battery { get; set; } = new BatteryConfig();

        public MachineAuthResponse()
        {
            Type = MessageTypes.MachineAuthResponse;
        }
    }

    /// <summary>Lanzador de flota -> servidor: pide la lista de máquinas de una explotación.</summary>
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
    /// Máquina -> servidor: "dame trabajo". El hilo del servidor que atiende a esta máquina
    /// se queda en Monitor.Wait() hasta que haya una tarea adecuada para ella.
    /// </summary>
    public class TaskRequest : NetMessage
    {
        public string MachineId { get; set; } = "";

        /// <summary>Milisegundos máximos de espera (0 = esperar lo que haga falta).</summary>
        public int MaxWaitMs { get; set; }

        public TaskRequest()
        {
            Type = MessageTypes.TaskRequest;
        }
    }

    /// <summary>Servidor -> máquina: respuesta a TASK_REQUEST.</summary>
    public class TaskAssignment : ResponseMessage
    {
        public AssignmentOutcome Outcome { get; set; }
        public string MachineId { get; set; }
        public string TaskId { get; set; }
        public string FieldId { get; set; }
        public int FieldIndex { get; set; }
        public TaskType TaskType { get; set; }
        public CropType Crop { get; set; }

        /// <summary>Progreso ya hecho (si otra máquina la dejó a medias).</summary>
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

    /// <summary>La máquina deja una tarea a medias (batería baja, orden de volver...).</summary>
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

    /// <summary>Servidor -> máquina: el operador ha cancelado la tarea que estaba haciendo.</summary>
    public class TaskCancelledNotice : NetMessage
    {
        public string TaskId { get; set; } = "";
        public string Reason { get; set; } = "";

        public TaskCancelledNotice()
        {
            Type = MessageTypes.TaskCancelled;
        }
    }

    /// <summary>Máquina -> servidor: quiere entrar en una zona del camino.</summary>
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

    /// <summary>Máquina -> servidor: ya ha salido de la zona.</summary>
    public class ZoneRelease : NetMessage
    {
        public string MachineId { get; set; } = "";
        public string ZoneId { get; set; } = "";

        public ZoneRelease()
        {
            Type = MessageTypes.ZoneRelease;
        }
    }

    /// <summary>Máquina -> servidor: necesita un cargador (espera en cola si no hay ninguno libre).</summary>
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

    /// <summary>Máquina -> servidor: algo importante ha pasado (batería baja, vuelve a la base...).</summary>
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
