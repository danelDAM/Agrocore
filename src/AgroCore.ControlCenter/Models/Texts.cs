using System.Windows.Media;
using AgroCore.Shared.Enums;

namespace AgroCore.ControlCenter.Models
{
    /// <summary>Textos en castellano y colores para mostrar los estados en pantalla.</summary>
    public static class Texts
    {
        public static string MachineType(MachineType type)
        {
            switch (type)
            {
                case AgroCore.Shared.Enums.MachineType.Seeder: return "Sembradora";
                case AgroCore.Shared.Enums.MachineType.Irrigator: return "Regadora";
                case AgroCore.Shared.Enums.MachineType.Sprayer: return "Dron fumigador";
                case AgroCore.Shared.Enums.MachineType.Harvester: return "Cosechadora";
            }
            return type.ToString();
        }

        public static string MachineStatus(MachineStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.MachineStatus.Offline: return "Desconectada";
                case AgroCore.Shared.Enums.MachineStatus.Idle: return "Disponible";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForTask: return "Esperando tarea";
                case AgroCore.Shared.Enums.MachineStatus.MovingToField: return "Hacia el campo";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForZone: return "Esperando paso";
                case AgroCore.Shared.Enums.MachineStatus.Yielding: return "Cediendo el paso";
                case AgroCore.Shared.Enums.MachineStatus.Working: return "Trabajando";
                case AgroCore.Shared.Enums.MachineStatus.ReturningToBase: return "Volviendo a base";
                case AgroCore.Shared.Enums.MachineStatus.WaitingForCharger: return "Esperando cargador";
                case AgroCore.Shared.Enums.MachineStatus.Charging: return "Cargando";
                case AgroCore.Shared.Enums.MachineStatus.Parked: return "Aparcada";
                case AgroCore.Shared.Enums.MachineStatus.Stopped: return "Detenida";
                case AgroCore.Shared.Enums.MachineStatus.Emergency: return "EMERGENCIA";
            }
            return status.ToString();
        }

        public static Color StatusColor(MachineStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.MachineStatus.Working:
                    return Color.FromRgb(93, 187, 99);    // verde
                case AgroCore.Shared.Enums.MachineStatus.MovingToField:
                case AgroCore.Shared.Enums.MachineStatus.ReturningToBase:
                    return Color.FromRgb(79, 163, 224);   // azul
                case AgroCore.Shared.Enums.MachineStatus.Charging:
                    return Color.FromRgb(64, 210, 200);   // turquesa
                case AgroCore.Shared.Enums.MachineStatus.WaitingForZone:
                case AgroCore.Shared.Enums.MachineStatus.WaitingForCharger:
                case AgroCore.Shared.Enums.MachineStatus.Yielding:
                case AgroCore.Shared.Enums.MachineStatus.WaitingForTask:
                    return Color.FromRgb(227, 166, 59);   // naranja
                case AgroCore.Shared.Enums.MachineStatus.Stopped:
                case AgroCore.Shared.Enums.MachineStatus.Emergency:
                    return Color.FromRgb(224, 82, 74);    // rojo
                case AgroCore.Shared.Enums.MachineStatus.Offline:
                    return Color.FromRgb(90, 100, 95);    // gris
            }
            return Color.FromRgb(200, 210, 205);          // disponible / aparcada
        }

        public static string TaskType(TaskType type)
        {
            switch (type)
            {
                case AgroCore.Shared.Enums.TaskType.Sow: return "Siembra";
                case AgroCore.Shared.Enums.TaskType.Growth: return "Crecimiento";
                case AgroCore.Shared.Enums.TaskType.Irrigate: return "Riego";
                case AgroCore.Shared.Enums.TaskType.Spray: return "Fumigación";
                case AgroCore.Shared.Enums.TaskType.Harvest: return "Cosecha";
            }
            return type.ToString();
        }

        public static string TaskStatus(AgroTaskStatus status)
        {
            switch (status)
            {
                case AgroTaskStatus.Pending: return "Pendiente";
                case AgroTaskStatus.Waiting: return "En espera";
                case AgroTaskStatus.Assigned: return "Asignada";
                case AgroTaskStatus.Running: return "En curso";
                case AgroTaskStatus.Completed: return "Completada";
                case AgroTaskStatus.Cancelled: return "Cancelada";
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
            return Color.FromRgb(120, 135, 128); // completada
        }

        public static string FieldStatus(FieldStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.FieldStatus.Empty: return "Vacío";
                case AgroCore.Shared.Enums.FieldStatus.Sowing: return "Sembrando";
                case AgroCore.Shared.Enums.FieldStatus.Growing: return "Creciendo";
                case AgroCore.Shared.Enums.FieldStatus.ReadyToHarvest: return "Listo para cosechar";
                case AgroCore.Shared.Enums.FieldStatus.Harvesting: return "Cosechando";
                case AgroCore.Shared.Enums.FieldStatus.Harvested: return "Cosechado";
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
                case CropType.Carrot: return "Zanahoria";
                case CropType.Corn: return "Maíz";
                case CropType.Tomato: return "Tomate";
            }
            return crop.Value.ToString();
        }

        public static string Role(UserRole role)
        {
            switch (role)
            {
                case UserRole.Admin: return "Administrador";
                case UserRole.Farmer: return "Agricultor";
                case UserRole.Operator: return "Operador";
                case UserRole.Viewer: return "Observador";
            }
            return role.ToString();
        }

        public static string BatteryStatus(BatteryStatus status)
        {
            switch (status)
            {
                case AgroCore.Shared.Enums.BatteryStatus.BatteryOk: return "Correcta";
                case AgroCore.Shared.Enums.BatteryStatus.LowBattery: return "Baja";
                case AgroCore.Shared.Enums.BatteryStatus.ReturningToBase: return "Volviendo a cargar";
                case AgroCore.Shared.Enums.BatteryStatus.WaitingForCharger: return "En cola de carga";
                case AgroCore.Shared.Enums.BatteryStatus.Charging: return "Cargando";
                case AgroCore.Shared.Enums.BatteryStatus.FullyCharged: return "Completa";
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
