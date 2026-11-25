using System;
using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Ustiategi baten soroa (lursaila).</summary>
    public class Field
    {
        /// <summary>FIELD-A, FIELD-B...</summary>
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Lursailaren posizioa planoan (0, 1, 2...).</summary>
        public int Index { get; set; }

        public FieldStatus Status { get; set; } = FieldStatus.Empty;

        /// <summary>Une honetan ereindako laborea.</summary>
        public CropType? Crop { get; set; }

        /// <summary>Planifikatutako laborea.</summary>
        public CropType? PlannedCrop { get; set; }

        /// <summary>True bada, uzta bildu ondoren gauza bera landatzen da berriro.</summary>
        public bool AutoReplant { get; set; }

        /// <summary>Hazkuntza 0tik 100era. Zerbitzariak kalkulatzen du.</summary>
        public double Growth { get; set; }

        /// <summary>Hazkuntza geldituta badago, zer behar duen (ureztatu edo fumigatu).</summary>
        public TaskType? GrowthBlockedBy { get; set; }

        /// <summary>Ziklo honetan dagoeneko egindako zainketak (ureztatzea, fumigazioa).</summary>
        public List<TaskType> CompletedCare { get; set; } = new List<TaskType>();

        public string CurrentTaskId { get; set; }
        public DateTime? PlantedAt { get; set; }
        public DateTime? HarvestedAt { get; set; }
        public int TimesHarvested { get; set; }

        /// <summary>Soroaren kopia (jatorrizko objektua harien artean partekatu gabe bidaltzeko).</summary>
        public Field Clone()
        {
            Field copy = (Field)MemberwiseClone();
            copy.CompletedCare = new List<TaskType>(CompletedCare);
            return copy;
        }
    }
}
