using System;
using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Tarea agrícola (el "Task" del enunciado). Se llama FarmTask para no confundirla
    /// con System.Threading.Tasks.Task.
    /// </summary>
    public class FarmTask
    {
        /// <summary>TASK-101, TASK-102...</summary>
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public string FieldId { get; set; } = "";
        public TaskType Type { get; set; }
        public CropType Crop { get; set; }
        public AgroTaskStatus Status { get; set; } = AgroTaskStatus.Waiting;
        public string AssignedMachineId { get; set; }

        /// <summary>Tareas que tienen que estar COMPLETED antes de empezar esta.</summary>
        public List<string> DependsOn { get; set; } = new List<string>();

        /// <summary>Crecimiento mínimo del campo para poder hacerla (riego y fumigación).</summary>
        public double? RequiredGrowth { get; set; }

        /// <summary>Por qué está esperando (se muestra en la interfaz).</summary>
        public string BlockReason { get; set; }

        /// <summary>Progreso 0-100. Si se interrumpe, otra máquina sigue desde aquí.</summary>
        public double Progress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; }
        public string CancelReason { get; set; }
        public int Interruptions { get; set; }

        /// <summary>Nombre legible, por ejemplo HARVEST_FIELD-A.</summary>
        public string Name
        {
            get { return Type.ToString().ToUpper() + "_" + FieldId; }
        }

        public bool IsFinished()
        {
            return Status == AgroTaskStatus.Completed || Status == AgroTaskStatus.Cancelled;
        }

        public FarmTask Clone()
        {
            FarmTask copy = (FarmTask)MemberwiseClone();
            copy.DependsOn = new List<string>(DependsOn);
            return copy;
        }
    }
}
