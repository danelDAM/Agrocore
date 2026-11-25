namespace AgroCore.Shared.Enums;

public enum MachineStatus
{
    Offline,
    Idle,
    WaitingForTask,
    MovingToField,
    WaitingForZone,
    /// <summary>Cede el paso (apartada en el arcén) tras evitarse un interbloqueo de zonas.</summary>
    Yielding,
    Working,
    ReturningToBase,
    WaitingForCharger,
    Charging,
    /// <summary>Aparcada en la base tras un RETURN_TO_BASE manual; espera START.</summary>
    Parked,
    Stopped,
    Emergency
}
