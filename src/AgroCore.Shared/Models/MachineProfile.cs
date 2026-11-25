namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Makina baten gaitasunak (bere "hardwarea"). Makinak zerbitzariari bidaltzen dizkio konektatzean
    /// eta zerbitzariak zeregin bakoitzerako makinarik egokiena aukeratzeko erabiltzen ditu.
    /// </summary>
    public class MachineProfile
    {
        public string Model { get; set; } = "";

        /// <summary>Mugitzean duen abiadura (unitateak segundoko).</summary>
        public double Speed { get; set; } = 4;

        /// <summary>Lanean ari den bitartean motelago doa: abiadura = Speed x WorkSpeedFactor.</summary>
        public double WorkSpeedFactor { get; set; } = 0.5;

        /// <summary>Bateria-kontsumoa mugitzean (% segundoko).</summary>
        public double MoveConsumption { get; set; } = 0.25;

        /// <summary>Bateria-kontsumoa lan egitean (% segundoko).</summary>
        public double WorkConsumption { get; set; } = 0.45;

        /// <summary>Edukiera (tolvaren kg-ak, depositoaren litroak...).</summary>
        public double Capacity { get; set; } = 100;

        /// <summary>Lan-erradioa. Pasada bakoitzaren zabalera 2 x WorkRadius da.</summary>
        public double WorkRadius { get; set; } = 1.5;

        /// <summary>Eraginkortasuna 0 eta 1 artean.</summary>
        public double Efficiency { get; set; } = 0.9;
    }
}
