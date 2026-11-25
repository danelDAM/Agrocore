namespace AgroCore.Shared.Enums;

/// <summary>Órdenes individuales que el centro de control puede enviar a una máquina (vía servidor).</summary>
public enum MachineCommandType
{
    Start,
    Stop,
    Resume,
    ReturnToBase
}

/// <summary>Órdenes globales sobre toda la flota de una explotación.</summary>
public enum FarmCommandType
{
    StopAll,
    ResumeAll,
    ReturnAllToBase,
    EmergencyStop
}
