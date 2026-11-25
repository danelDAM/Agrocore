using System.Windows.Media;
using AgroCore.Shared.Enums;

namespace AgroCore.ControlCenter.Models
{
    /// <summary>Euskarazko testuak eta koloreak, egoerak pantailan erakusteko.</summary>
    public static class Texts
    {
        public static string MachineType(MachineType type)
        {
            switch (type)
            {
                case AgroCore.Shared.Enums.MachineType.Seeder: return "Ereilea";
                case AgroCore.Shared.Enums.MachineType.Irrigator: return "Ureztatzailea";
                case AgroCore.Shared.Enums.MachineType.Sprayer: return "Fumigazio-dronea";
                case AgroCore.Shared.Enums.MachineType.Harvester: return "Uzta-biltzailea";
            }
            return type.ToString();
        }

        public static string MachineStatus(MachineStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.MachineStatus.Offline: return "Deskonektatuta";
                case AgroCore.Shared.Enums.MachineStatus.Idle: return "Erabilgarri";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForTask: return "Zereginaren zain";
                case AgroCore.Shared.Enums.MachineStatus.MovingToField: return "Sororantz";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForZone: return "Pasabidearen zain";
                case AgroCore.Shared.Enums.MachineStatus.Yielding: return "Bidea uzten";
                case AgroCore.Shared.Enums.MachineStatus.Working: return "Lanean";
                case AgroCore.Shared.Enums.MachineStatus.ReturningToBase: return "Basera itzultzen";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForCharger: return "Kargagailuaren zain";
                case AgroCore.Shared.Enums.MachineStatus.Charging: return "Kargatzen";
                case AgroCore.Shared.Enums.MachineStatus.Parked: return "Aparkatuta";
                case AgroCore.Shared.Enums.MachineStatus.Stopped: return "Geldituta";
                case AgroCore.Shared.Enums.MachineStatus.Emergency: return "LARRIALDIA";
            }
            return status.ToString();
        }

        public static Color StatusColor(MachineStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.MachineStatus.Working:
                    return Color.FromRgb(93, 187, 99);    // berdea
                case AgroCore.Shared.Enums.MachineStatus.MovingToField:
                case AgroCore.Shared.Enums.MachineStatus.ReturningToBase:
                    return Color.FromRgb(79, 163, 224);   // urdina
                case AgroCore.Shared.Enums.MachineStatus.Charging:
                    return Color.FromRgb(64, 210, 200);   // turkesa
                case AgroCore.Shared.Enums.MachineStatus.WaitingForZone:
                case AgroCore.Shared.Enums.MachineStatus.WaitingForCharger:
                case AgroCore.Shared.Enums.MachineStatus.Yielding:
                case AgroCore.Shared.Enums.MachineStatus.WaitingForTask:
                    return Color.FromRgb(227, 166, 59);   // laranja
                case AgroCore.Shared.Enums.MachineStatus.Stopped:
                case AgroCore.Shared.Enums.MachineStatus.Emergency:
                    return Color.FromRgb(224, 82, 74);    // gorria
                case AgroCore.Shared.Enums.MachineStatus.Offline:
                    return Color.FromRgb(90, 100, 95);    // grisa
            }
            return Color.FromRgb(200, 210, 205);          // erabilgarri / aparkatuta
        }

        public static string TaskType(TaskType type)
        {
            switch (type)
            {
                case AgroCore.Shared.Enums.TaskType.Sow: return "Ereitea";
                case AgroCore.Shared.Enums.TaskType.Growth: return "Hazkuntza";
                case AgroCore.Shared.Enums.TaskType.Irrigate: return "Ureztatzea";
                case AgroCore.Shared.Enums.TaskType.Spray: return "Fumigazioa";
                case AgroCore.Shared.Enums.TaskType.Harvest: return "Uzta-bilketa";
            }
            return type.ToString();
        }

        public static string TaskStatus(AgroTaskStatus status)
        {
            switch (status)
            {
                case AgroTaskStatus.Pending: return "Egiteko";
                case AgroTaskStatus.Waiting: return "Zain";
                case AgroTaskStatus.Assigned: return "Esleituta";
                case AgroTaskStatus.Running: return "Martxan";
                case AgroTaskStatus.Completed: return "Osatuta";
                case AgroTaskStatus.Cancelled: return "Bertan behera utzita";
            }
            return status.ToString();
        }

        public static Color TaskStatusColor(AgroTaskStatus status)
        {
            switch (status)
            {
                case AgroTaskStatus.Running: return Color.FromRgb(93, 187, 99);
                case AgroTaskStatus.Assigned: return Color.FromRgb(79, 163, 224);
                case AgroTaskStatus.Pending: return Color.FromRgb(200, 210, 205);
                case AgroTaskStatus.Waiting: return Color.FromRgb(227, 166, 59);
                case AgroTaskStatus.Cancelled: return Color.FromRgb(224, 82, 74);
            }
            return Color.FromRgb(120, 135, 128); // osatuta
        }

        public static string FieldStatus(FieldStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.FieldStatus.Empty: return "Hutsik";
                case AgroCore.Shared.Enums.FieldStatus.Sowing: return "Ereiten";
                case AgroCore.Shared.Enums.FieldStatus.Growing: return "Hazten";
                case AgroCore.Shared.Enums.FieldStatus.ReadyToHarvest: return "Uzta biltzeko prest";
                case AgroCore.Shared.Enums.FieldStatus.Harvesting: return "Uzta biltzen";
                case AgroCore.Shared.Enums.FieldStatus.Harvested: return "Uzta bilduta";
            }
            return status.ToString();
        }

        public static Color FieldStatusColor(FieldStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.FieldStatus.Sowing: return Color.FromRgb(79, 163, 224);
                case AgroCore.Shared.Enums.FieldStatus.Growing: return Color.FromRgb(93, 187, 99);
                case AgroCore.Shared.Enums.FieldStatus.ReadyToHarvest: return Color.FromRgb(240, 200, 60);
                case AgroCore.Shared.Enums.FieldStatus.Harvesting: return Color.FromRgb(227, 140, 59);
                case AgroCore.Shared.Enums.FieldStatus.Harvested: return Color.FromRgb(170, 140, 90);
            }
            return Color.FromRgb(120, 135, 128);
        }

        public static string Crop(CropType? crop)
        {
            if (!crop.HasValue)
            {
                return "—";
            }
            switch (crop.Value)
            {
                case CropType.Carrot: return "Azenarioa";
                case CropType.Corn: return "Artoa";
                case CropType.Tomato: return "Tomatea";
            }
            return crop.Value.ToString();
        }

        public static string Role(UserRole role)
        {
            switch (role)
            {
                case UserRole.Admin: return "Administratzailea";
                case UserRole.Farmer: return "Nekazaria";
                case UserRole.Operator: return "Operadorea";
                case UserRole.Viewer: return "Behatzailea";
            }
            return role.ToString();
        }

        public static string BatteryStatus(BatteryStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.BatteryStatus.BatteryOk: return "Ondo";
                case AgroCore.Shared.Enums.BatteryStatus.LowBattery: return "Baxua";
                case AgroCore.Shared.Enums.BatteryStatus.ReturningToBase: return "Kargatzera itzultzen";
                case AgroCore.Shared.Enums.BatteryStatus.WaitingForCharger: return "Karga-ilaran";
                case AgroCore.Shared.Enums.BatteryStatus.Charging: return "Kargatzen";
                case AgroCore.Shared.Enums.BatteryStatus.FullyCharged: return "Beteta";
            }
            return status.ToString();
        }

        public static Brush ToBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static Color BatteryColor(double battery)
        {
            if (battery < 25)
            {
                return Color.FromRgb(224, 82, 74);
            }
            if (battery < 50)
            {
                return Color.FromRgb(227, 166, 59);
            }
            return Color.FromRgb(93, 187, 99);
        }
    }
}
