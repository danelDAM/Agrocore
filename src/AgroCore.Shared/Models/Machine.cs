using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Estado de una máquina tal y como lo conoce el servidor (que es la autoridad).</summary>
    public class Machine
    {
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public string Name { get; set; } = "";
        public MachineType Type { get; set; }

        public Position Position { get; set; } = new Position();

        /// <summary>Hacia dónde mira, en grados.</summary>
        public double Heading { get; set; }

        public double Speed { get; set; }
        public double Battery { get; set; }
        public MachineStatus Status { get; set; } = MachineStatus.Offline;
        public BatteryStatus BatteryStatus { get; set; } = BatteryStatus.BatteryOk;

        public string CurrentTaskId { get; set; }
        public TaskType? CurrentTaskType { get; set; }
        public string CurrentFieldId { get; set; }
        public double TaskProgress { get; set; }

        public double Temperature { get; set; }
        public bool Online { get; set; }

        /// <summary>Explicación corta del estado, por ejemplo "esperando zona Z07-04".</summary>
        public string Note { get; set; }

        public MachineProfile Profile { get; set; } = new MachineProfile();
    }
}
