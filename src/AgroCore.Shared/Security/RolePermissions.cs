using AgroCore.Shared.Enums;

namespace AgroCore.Shared.Security
{
    /// <summary>Baimena behar duten ekintzak.</summary>
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
    /// Rol bakoitzak zer egin dezakeen. ZERBITZARIAK taula hau erabiltzen du eskaera bakoitza onartu edo ukatzeko.
    /// Kontrol Zentroak botoiak ezkutatzeko soilik erabiltzen du (hori ez da segurtasuna, erosotasuna baizik).
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
                return true; // administratzaileak dena egin dezake
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
                case UserRole.Admin: return "Administratzailea";
                case UserRole.Farmer: return "Nekazaria";
                case UserRole.Operator: return "Operadorea";
                case UserRole.Viewer: return "Behatzailea";
                default: return role.ToString();
            }
        }
    }
}
