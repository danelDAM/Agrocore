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

/// <summary>Erregistratutako gertaera mota. Estatistikak gertaeren erregistroko gertaera hauek zenbatuz kalkulatzen dira.</summary>
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
