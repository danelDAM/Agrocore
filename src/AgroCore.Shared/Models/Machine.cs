using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Makina baten egoera, zerbitzariak ezagutzen duen bezala (bera da agintea).</summary>
    public class Machine
    {
        public string Id { get; set; } = "";
        public int FarmId { get; set; }
        public string Name { get; set; } = "";
        public MachineType Type { get; set; }

        public Position Position { get; set; } = new Position();

        /// <summary>Nora begira dagoen, gradutan.</summary>
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

        /// <summary>Egoeraren azalpen laburra, adibidez "Z07-04 gunearen zain".</summary>
        public string Note { get; set; }

        public MachineProfile Profile { get; set; } = new MachineProfile();
    }
}
