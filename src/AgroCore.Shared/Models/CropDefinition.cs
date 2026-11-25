using System.Collections.Generic;
using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Cuidado que necesita un cultivo al llegar a cierto crecimiento.</summary>
    public class CareStep
    {
        public TaskType Task { get; set; }

        /// <summary>El cultivo deja de crecer en este porcentaje hasta que se hace el cuidado.</summary>
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

    /// <summary>Definición de un tipo de cultivo. Se puede cambiar desde la configuración.</summary>
    public class CropDefinition
    {
        public CropType Type { get; set; }
        public string DisplayName { get; set; } = "";

        /// <summary>Minutos que tarda en crecer del 0 al 100 %.</summary>
        public double GrowthMinutes { get; set; }

        public List<CareStep> CareSteps { get; set; } = new List<CareStep>();

        /// <summary>Kilos que produce una parcela.</summary>
        public double Yield { get; set; } = 150;

        /// <summary>Los tres cultivos iniciales.</summary>
        public static List<CropDefinition> Defaults()
        {
            List<CropDefinition> list = new List<CropDefinition>();

            // Zanahoria: sembrar + regar + recolectar
            CropDefinition carrot = new CropDefinition();
            carrot.Type = CropType.Carrot;
            carrot.DisplayName = "Zanahorias";
            carrot.GrowthMinutes = 5;
            carrot.Yield = 120;
            carrot.CareSteps.Add(new CareStep(TaskType.Irrigate, 35));
            list.Add(carrot);

            // Maíz: sembrar + regar + fumigar + recolectar
            CropDefinition corn = new CropDefinition();
            corn.Type = CropType.Corn;
            corn.DisplayName = "Maíz";
            corn.GrowthMinutes = 8;
            corn.Yield = 220;
            corn.CareSteps.Add(new CareStep(TaskType.Irrigate, 25));
            corn.CareSteps.Add(new CareStep(TaskType.Spray, 60));
            list.Add(corn);

            // Tomate: sembrar + fumigar + regar + recolectar
            CropDefinition tomato = new CropDefinition();
            tomato.Type = CropType.Tomato;
            tomato.DisplayName = "Tomates";
            tomato.GrowthMinutes = 12;
            tomato.Yield = 180;
            tomato.CareSteps.Add(new CareStep(TaskType.Spray, 20));
            tomato.CareSteps.Add(new CareStep(TaskType.Irrigate, 50));
            list.Add(tomato);

            return list;
        }
    }
}
