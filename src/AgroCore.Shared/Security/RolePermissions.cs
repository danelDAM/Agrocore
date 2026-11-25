using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Security
{
    /// <summary>Acciones que necesitan permiso.</summary>
    public enum Permission
    {
        ViewFarm,
        ViewAllFarms,
        ControlMachines,
        GlobalCommands,
        EmergencyStop,
        ManageCropPlans,
        ManageTasks,
        ViewStatistics,
        ViewGlobalStatistics,
        GenerateReports,
        ViewLogs,
        ManageUsers,
        ConfigureSystem,
        ChangeOwnPassword
    }

    /// <summary>
    /// Qué puede hacer cada rol. El SERVIDOR usa esta tabla para aceptar o rechazar cada petición.
    /// El centro de control la usa solo para esconder botones (eso no es seguridad, es comodidad).
    /// </summary>
    public static class RolePermissions
    {
        private static readonly Permission[] FarmerPermissions =
        {
            Permission.ViewFarm,
            Permission.ControlMachines,
            Permission.GlobalCommands,
            Permission.EmergencyStop,
            Permission.ManageCropPlans,
            Permission.ManageTasks,
            Permission.ViewStatistics,
            Permission.GenerateReports,
            Permission.ViewLogs,
            Permission.ChangeOwnPassword
        };

        private static readonly Permission[] OperatorPermissions =
        {
            Permission.ViewFarm,
            Permission.ControlMachines,
            Permission.GlobalCommands,
            Permission.EmergencyStop,
            Permission.ViewStatistics,
            Permission.ViewLogs,
            Permission.ChangeOwnPassword
        };

        private static readonly Permission[] ViewerPermissions =
        {
            Permission.ViewFarm,
            Permission.ViewStatistics,
            Permission.ChangeOwnPassword
        };

        public static bool Has(UserRole role, Permission permission)
        {
            if (role == UserRole.Admin)
            {
                return true; // el administrador puede hacerlo todo
            }

            Permission[] allowed;
            if (role == UserRole.Farmer)
            {
                allowed = FarmerPermissions;
            }
            else if (role == UserRole.Operator)
            {
                allowed = OperatorPermissions;
            }
            else
            {
                allowed = ViewerPermissions;
            }

            foreach (Permission p in allowed)
            {
                if (p == permission)
                {
                    return true;
                }
            }
            return false;
        }

        public static string DisplayName(UserRole role)
        {
            switch (role)
            {
                case UserRole.Admin: return "Administrador";
                case UserRole.Farmer: return "Agricultor";
                case UserRole.Operator: return "Operador";
                case UserRole.Viewer: return "Observador";
                default: return role.ToString();
            }
        }
    }
}
