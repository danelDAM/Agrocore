using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Models
{
    /// <summary>Makina baten telemetria-datuak (zerbitzariak UDP bidez birbidaltzen dizkio Kontrol Zentroari).</summary>
    public class Telemetry
    {
        public string MachineId { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Heading { get; set; }
        public double Battery { get; set; }
        public double Speed { get; set; }
        public double Temperature { get; set; }
        public MachineStatus Status { get; set; }
        public BatteryStatus BatteryStatus { get; set; }
        public double TaskProgress { get; set; }
    }
}
