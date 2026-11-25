namespace AgroCore.Shared.Enums;

public enum MachineStatus
{
    Offline,
    Idle,
    WaitingForTask,
    MovingToField,
    WaitingForZone,
    /// <summary>Bidea uzten du (bazterrean geldituta) guneen elkarblokeo bat saihestu ondoren.</summary>
    Yielding,
    Working,
    ReturningToBase,
    WaitingForCharger,
    Charging,
    /// <summary>Basean aparkatuta eskuzko RETURN_TO_BASE baten ondoren; START-en zain.</summary>
    Parked,
    Stopped,
    Emergency
}
