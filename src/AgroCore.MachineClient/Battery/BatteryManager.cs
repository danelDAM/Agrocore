using AgroCore.MachineClient.Machine;
using AgroCore.Shared.Enums;
using AgroCore.Shared.Map;
using AgroCore.Shared.Models;

namespace AgroCore.MachineClient.Battery
{
    /// <summary>
    /// Makinaren bateria: mugitzean eta lan egitean kontsumoa, karga eta ERABAKIAK:
    ///  - Bateria baxua dut?
    ///  - Zeregina amaitu eta oraindik basera itzul naiteke?
    ///  - Basera itzultzeko bateria nahikoa dut?
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

        /// <summary>true itzultzen du BEHIN, bateria baxu bihurtzen denean (zerbitzariari abisatzeko).</summary>
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

        /// <summary>Posizio batetik basera itzultzeko behar den %, segurtasun-tartearekin.</summary>
        public double CostToReturn(Position from)
        {
            double distance = MachineMath.TravelDistance(type, from, MachineMath.BaseGate(layout)) + 12;
            return MachineMath.MoveCost(profile, config, distance) + config.ReturnReserve;
        }

        /// <summary>Geratzen zaion bateriarekin basera itzul daiteke?</summary>
        public bool CanReturnToBase(Position from)
        {
            return state.Battery >= CostToReturn(from);
        }

        /// <summary>Falta zaion lana amaitu eta gero basera itzul daiteke?</summary>
        public bool CanFinishAndReturn(Position from, double progressPercent)
        {
            double work = MachineMath.WorkCost(profile, config, MachineMath.WorkSeconds(profile, layout.FieldSize, progressPercent));
            return state.Battery >= work + CostToReturn(from);
        }

        /// <summary>BATTERY_OK / LOW_BATTERY adierazlea eguneratzen du (besteak karga-errutinak jartzen ditu).</summary>
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
