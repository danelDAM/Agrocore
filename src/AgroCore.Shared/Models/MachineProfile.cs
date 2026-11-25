namespace AgroCore.Shared.Models
{
    /// <summary>
    /// Capacidades de una máquina (su "hardware"). La máquina las envía al servidor al conectarse
    /// y el servidor las usa para elegir la máquina más adecuada para cada tarea.
    /// </summary>
    public class MachineProfile
    {
        public string Model { get; set; } = "";

        /// <summary>Velocidad al desplazarse (unidades por segundo).</summary>
        public double Speed { get; set; } = 4;

        /// <summary>Mientras trabaja va más despacio: velocidad = Speed x WorkSpeedFactor.</summary>
        public double WorkSpeedFactor { get; set; } = 0.5;

        /// <summary>Consumo de batería al desplazarse (% por segundo).</summary>
        public double MoveConsumption { get; set; } = 0.25;

        /// <summary>Consumo de batería al trabajar (% por segundo).</summary>
        public double WorkConsumption { get; set; } = 0.45;

        /// <summary>Capacidad (kg de tolva, litros de depósito...).</summary>
        public double Capacity { get; set; } = 100;

        /// <summary>Radio de trabajo. El ancho de cada pasada es 2 x WorkRadius.</summary>
        public double WorkRadius { get; set; } = 1.5;

        /// <summary>Eficiencia entre 0 y 1.</summary>
        public double Efficiency { get; set; } = 0.9;
    }
}
