using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Laborea hazkuntza jakin batera iristean behar duen zainketa.</summary>
    public class CareStep
    {
        public TaskType Task { get; set; }

        /// <summary>Laboreak ehuneko honetan uzten dio hazteari zainketa egin arte.</summary>
        public double AtGrowth { get; set; }

        public CareStep()
        {
        }

        public CareStep(TaskType task, double atGrowth)
        {
            Task = task;
            AtGrowth = atGrowth;
        }
    }

    /// <summary>Labore mota baten definizioa. Konfiguraziotik alda daiteke.</summary>
    public class CropDefinition
    {
        public CropType Type { get; set; }
        public string DisplayName { get; set; } = "";

        /// <summary>0tik 100 %-ra hazteko behar dituen minutuak.</summary>
        public double GrowthMinutes { get; set; }

        public List<CareStep> CareSteps { get; set; } = new List<CareStep>();

        /// <summary>Lursail batek ematen dituen kiloak.</summary>
        public double Yield { get; set; } = 150;

        /// <summary>Hasierako hiru laboreak.</summary>
        public static List<CropDefinition> Defaults()
        {
            List<CropDefinition> list = new List<CropDefinition>();

            // Azenarioa: erein + ureztatu + uzta bildu
            CropDefinition carrot = new CropDefinition();
            carrot.Type = CropType.Carrot;
            carrot.DisplayName = "Azenarioa";
            carrot.GrowthMinutes = 5;
            carrot.Yield = 120;
            carrot.CareSteps.Add(new CareStep(TaskType.Irrigate, 35));
            list.Add(carrot);

            // Artoa: erein + ureztatu + fumigatu + uzta bildu
            CropDefinition corn = new CropDefinition();
            corn.Type = CropType.Corn;
            corn.DisplayName = "Artoa";
            corn.GrowthMinutes = 8;
            corn.Yield = 220;
            corn.CareSteps.Add(new CareStep(TaskType.Irrigate, 25));
            corn.CareSteps.Add(new CareStep(TaskType.Spray, 60));
            list.Add(corn);

            // Tomatea: erein + fumigatu + ureztatu + uzta bildu
            CropDefinition tomato = new CropDefinition();
            tomato.Type = CropType.Tomato;
            tomato.DisplayName = "Tomatea";
            tomato.GrowthMinutes = 12;
            tomato.Yield = 180;
            tomato.CareSteps.Add(new CareStep(TaskType.Spray, 20));
            tomato.CareSteps.Add(new CareStep(TaskType.Irrigate, 50));
            list.Add(tomato);

            return list;
        }
    }
}
