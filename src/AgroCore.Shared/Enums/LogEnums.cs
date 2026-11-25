namespace AgroCore.Shared.Enums;

public enum LogSeverity
{
    Debug,
    Info,
    Success,
    Warning,
    Error,
    Security
}

/// <summary>Tipo de evento registrado. Las estadísticas se calculan contando estos eventos en el log.</summary>
public enum EventKind
{
    Info,
    ServerStarted,
    ServerStopped,
    ClientConnected,
    ClientDisconnected,
    LoginSuccess,
    LoginFailed,
    Logout,
    AccessDenied,
    PasswordChanged,
    UserManagement,
    ConfigChanged,
    MachineAuthenticated,
    MachineAuthFailed,
    CropPlanCreated,
    TaskCreated,
    TaskAssigned,
    TaskStarted,
    TaskCompleted,
    TaskAborted,
    TaskCancelled,
    FieldStateChanged,
    MachineStarted,
    MachineStopped,
    MachineResumed,
    ReturnToBase,
    LowBattery,
    WaitingForCharger,
    ChargingStarted,
    ChargingCompleted,
    EmergencyStop,
    ZoneReserved,
    ZoneReleased,
    ZoneConflict,
    DeadlockAvoided,
    Error
}
