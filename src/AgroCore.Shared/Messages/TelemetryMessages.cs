using System.Collections.Generic;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Máquina -> servidor por UDP, 5 veces por segundo.
    /// Lleva posición, batería, velocidad, temperatura y estado en un solo datagrama.
    /// Si se pierde uno no pasa nada: enseguida llega el siguiente.
    /// </summary>
    public class PositionUpdate : NetMessage
    {
        public string MachineId { get; set; } = "";

        /// <summary>Token de la sesión de la máquina. Sirve para descartar datagramas falsos.</summary>
        public string SessionToken { get; set; } = "";

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
        public string Note { get; set; }

        public PositionUpdate()
        {
            Type = MessageTypes.PositionUpdate;
        }
    }

    /// <summary>Servidor -> WPF por UDP: telemetría de las máquinas de la explotación que el usuario puede ver.</summary>
    public class TelemetryBatch : NetMessage
    {
        public int FarmId { get; set; }
        public List<Telemetry> Items { get; set; } = new List<Telemetry>();

        public TelemetryBatch()
        {
            Type = MessageTypes.TelemetryBatch;
        }
    }
}
