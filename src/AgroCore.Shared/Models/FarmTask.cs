using System;
using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Nekazaritza-zeregina (enuntziatuko "Task"). FarmTask deitzen da
    /// System.Threading.Tasks.Task-ekin ez nahasteko.
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

        /// <summary>Hau hasi aurretik COMPLETED egon behar duten zereginak.</summary>
        public List<string> DependsOn { get; set; } = new List<string>();

        /// <summary>Hura egin ahal izateko soroaren gutxieneko hazkuntza (ureztatzea eta fumigazioa).</summary>
        public double? RequiredGrowth { get; set; }

        /// <summary>Zergatik dagoen zain (interfazean erakusten da).</summary>
        public string BlockReason { get; set; }

        /// <summary>Aurrerapena 0-100. Eteten bada, beste makina batek hemendik jarraitzen du.</summary>
        public double Progress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; }
        public string CancelReason { get; set; }
        public int Interruptions { get; set; }

        /// <summary>Izen irakurgarria, adibidez HARVEST_FIELD-A.</summary>
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
