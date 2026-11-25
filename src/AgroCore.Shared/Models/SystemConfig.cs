using System.Collections.Generic;

namespace AgroCore.Shared.Models
{
    /// <summary>Valores de batería (los puede cambiar el administrador).</summary>
    public class BatteryConfig
    {
        /// <summary>Multiplica el consumo al desplazarse (1 = normal).</summary>
        public double MoveMultiplier { get; set; } = 1.0;

        /// <summary>Multiplica el consumo al trabajar (1 = normal).</summary>
        public double WorkMultiplier { get; set; } = 1.0;

        /// <summary>Cuánto se carga por segundo (%).</summary>
        public double ChargeRatePerSecond { get; set; } = 2.5;

        /// <summary>Por debajo de este % la batería se considera baja.</summary>
        public double LowBatteryThreshold { get; set; } = 25;

        /// <summary>% que siempre tiene que sobrar al llegar a la base.</summary>
        public double ReturnReserve { get; set; } = 6;
    }

    /// <summary>Configuración del sistema (fichero server-config.json).</summary>
    public class SystemConfig
    {
        public int TcpPort { get; set; } = 5000;
        public int UdpPort { get; set; } = 5001;

        /// <summary>Cada cuántos milisegundos se envía telemetría UDP.</summary>
        public int TelemetryIntervalMs { get; set; } = 200;

        /// <summary>Cada cuántos milisegundos se envía el estado completo (FARM_STATE) por TCP.</summary>
        public int StateBroadcastIntervalMs { get; set; } = 1000;

        /// <summary>Segundos que un campo se queda RECOLECTADO antes de quedar libre.</summary>
        public int HarvestedResetSeconds { get; set; } = 15;

        /// <summary>Intentos de login fallidos antes de bloquear el usuario un rato.</summary>
        public int MaxLoginAttempts { get; set; } = 5;
        public int LockoutSeconds { get; set; } = 30;

        public BatteryConfig Battery { get; set; } = new BatteryConfig();
        public List<CropDefinition> Crops { get; set; } = CropDefinition.Defaults();
    }
}
