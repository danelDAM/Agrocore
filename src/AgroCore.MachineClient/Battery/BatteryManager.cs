using AgroCore.MachineClient.Machine;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.MachineClient.Battery
{
    /// <summary>
    /// Batería de la máquina: consumo al moverse y trabajar, carga, y DECISIONES:
    ///  - ¿Tengo batería baja?
    ///  - ¿Puedo terminar la tarea y aún volver a la base?
    ///  - ¿Tengo batería suficiente para volver a la base?
    /// </summary>
    public class BatteryManager
    {
        private readonly MachineState state;
        private readonly MachineProfile profile;
        private readonly BatteryConfig config;
        private readonly FarmLayout layout;
        private readonly MachineType type;
        private bool lowBatteryReported;

        public BatteryManager(MachineState state, MachineProfile profile, BatteryConfig config, FarmLayout layout, MachineType type)
        {
            this.state = state;
            this.profile = profile;
            this.config = config;
            this.layout = layout;
            this.type = type;
        }

        public double Level
        {
            get { return state.Battery; }
        }

        public double ChargeRate
        {
            get { return config.ChargeRatePerSecond; }
        }

        public void ConsumeMoving(double seconds)
        {
            state.Battery = state.Battery - profile.MoveConsumption * config.MoveMultiplier * seconds;
            UpdateStatus();
        }

        public void ConsumeWorking(double seconds)
        {
            state.Battery = state.Battery - profile.WorkConsumption * config.WorkMultiplier * seconds;
            UpdateStatus();
        }

        public void Charge(double seconds)
        {
            state.Battery = state.Battery + config.ChargeRatePerSecond * seconds;
        }

        public bool IsLow()
        {
            return state.Battery <= config.LowBatteryThreshold;
        }

        /// <summary>Devuelve true UNA vez cuando la batería pasa a estar baja (para avisar al servidor).</summary>
        public bool JustBecameLow()
        {
            if (IsLow() && !lowBatteryReported)
            {
                lowBatteryReported = true;
                return true;
            }
            if (!IsLow())
            {
                lowBatteryReported = false;
            }
            return false;
        }

        /// <summary>% necesario para volver desde una posición a la base, con margen de seguridad.</summary>
        public double CostToReturn(Position from)
        {
            double distance = MachineMath.TravelDistance(type, from, MachineMath.BaseGate(layout)) + 12;
            return MachineMath.MoveCost(profile, config, distance) + config.ReturnReserve;
        }

        /// <summary>¿Puede volver a la base con la batería que le queda?</summary>
        public bool CanReturnToBase(Position from)
        {
            return state.Battery >= CostToReturn(from);
        }

        /// <summary>¿Puede terminar el trabajo que le falta y después volver a la base?</summary>
        public bool CanFinishAndReturn(Position from, double progressPercent)
        {
            double work = MachineMath.WorkCost(profile, config, MachineMath.WorkSeconds(profile, layout.FieldSize, progressPercent));
            return state.Battery >= work + CostToReturn(from);
        }

        /// <summary>Actualiza el indicador BATTERY_OK / LOW_BATTERY (los demás los pone la rutina de carga).</summary>
        private void UpdateStatus()
        {
            BatteryStatus current = state.BatteryStatus;
            if (current == BatteryStatus.ReturningToBase || current == BatteryStatus.WaitingForCharger || current == BatteryStatus.Charging)
            {
                return;
            }
            state.BatteryStatus = IsLow() ? BatteryStatus.LowBattery : BatteryStatus.BatteryOk;
        }
    }
}
