namespace AgroCore.Shared.Enums;

/// <summary>Roles del sistema. El servidor es quien aplica los permisos (ver <see cref="Security.RolePermissions"/>).</summary>
public enum UserRole
{
    Admin,
    Farmer,
    Operator,
    Viewer
}
