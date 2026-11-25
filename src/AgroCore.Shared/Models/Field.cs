using System;
using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Campo (parcela) de una explotación.</summary>
    public class Field
    {
        /// <summary>FIELD-A, FIELD-B...</summary>
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Posición de la parcela en el plano (0, 1, 2...).</summary>
        public int Index { get; set; }

        public FieldStatus Status { get; set; } = FieldStatus.Empty;

        /// <summary>Cultivo sembrado ahora mismo.</summary>
        public CropType? Crop { get; set; }

        /// <summary>Cultivo planificado.</summary>
        public CropType? PlannedCrop { get; set; }

        /// <summary>Si es true, después de recolectar se vuelve a plantar lo mismo.</summary>
        public bool AutoReplant { get; set; }

        /// <summary>Crecimiento de 0 a 100. Lo calcula el servidor.</summary>
        public double Growth { get; set; }

        /// <summary>Si el crecimiento está parado, qué necesita (regar o fumigar).</summary>
        public TaskType? GrowthBlockedBy { get; set; }

        /// <summary>Cuidados ya hechos en este ciclo (riego, fumigación).</summary>
        public List<TaskType> CompletedCare { get; set; } = new List<TaskType>();

        public string CurrentTaskId { get; set; }
        public DateTime? PlantedAt { get; set; }
        public DateTime? HarvestedAt { get; set; }
        public int TimesHarvested { get; set; }

        /// <summary>Copia del campo (para enviarlo sin compartir el objeto original entre hilos).</summary>
        public Field Clone()
        {
            Field copy = (Field)MemberwiseClone();
            copy.CompletedCare = new List<TaskType>(CompletedCare);
            return copy;
        }
    }
}
