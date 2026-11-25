using System.Collections.Generic;

namespace AgroCore.Shared.Models
{
    /// <summary>Bateria-balioak (administratzaileak alda ditzake).</summary>
    public class BatteryConfig
    {
        /// <summary>Mugitzean kontsumoa biderkatzen du (1 = normala).</summary>
        public double MoveMultiplier { get; set; } = 1.0;

        /// <summary>Lan egitean kontsumoa biderkatzen du (1 = normala).</summary>
        public double WorkMultiplier { get; set; } = 1.0;

        /// <summary>Segundoko zenbat kargatzen den (%).</summary>
        public double ChargeRatePerSecond { get; set; } = 2.5;

        /// <summary>% honen azpitik bateria baxutzat jotzen da.</summary>
        public double LowBatteryThreshold { get; set; } = 25;

        /// <summary>Basera iristean beti soberan geratu behar den %.</summary>
        public double ReturnReserve { get; set; } = 6;
    }

    /// <summary>Sistemaren konfigurazioa (server-config.json fitxategia).</summary>
    public class SystemConfig
    {
        public int TcpPort { get; set; } = 5000;
        public int UdpPort { get; set; } = 5001;

        /// <summary>Zenbat milisegundotik behin bidaltzen den UDP telemetria.</summary>
        public int TelemetryIntervalMs { get; set; } = 200;

        /// <summary>Zenbat milisegundotik behin bidaltzen den egoera osoa (FARM_STATE) TCP bidez.</summary>
        public int StateBroadcastIntervalMs { get; set; } = 1000;

        /// <summary>Soro bat UZTA BILDUTA egoeran geratzen den segundoak libre geratu aurretik.</summary>
        public int HarvestedResetSeconds { get; set; } = 15;

        /// <summary>Saio-hasierako huts egindako saiakerak erabiltzailea denbora batez blokeatu aurretik.</summary>
        public int MaxLoginAttempts { get; set; } = 5;
        public int LockoutSeconds { get; set; } = 30;

        public BatteryConfig Battery { get; set; } = new BatteryConfig();
        public List<CropDefinition> Crops { get; set; } = CropDefinition.Defaults();
    }
}
