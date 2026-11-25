using System.Collections.Generic;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Models;

namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Makina -> zerbitzaria UDP bidez, 5 aldiz segundoko.
    /// Posizioa, bateria, abiadura, tenperatura eta egoera datagrama bakar batean darama.
    /// Bat galtzen bada ez da ezer gertatzen: berehala iristen da hurrengoa.
    /// </summary>
    public class PositionUpdate : NetMessage
    {
        public string MachineId { get; set; } = "";

        /// <summary>Makinaren saioaren tokena. Datagrama faltsuak baztertzeko balio du.</summary>
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

    /// <summary>Zerbitzaria -> WPF UDP bidez: erabiltzaileak ikus dezakeen ustiategiko makinen telemetria.</summary>
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
