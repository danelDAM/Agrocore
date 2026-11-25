namespace AgroCore.Shared.Enums;

/// <summary>Kontrol Zentroak makina bati bidal diezazkiokeen agindu indibidualak (zerbitzariaren bidez).</summary>
public enum MachineCommandType
{
    Start,
    Stop,
    Resume,
    ReturnToBase
}

/// <summary>Ustiategi bateko flota osoaren gaineko agindu orokorrak.</summary>
public enum FarmCommandType
{
    StopAll,
    ResumeAll,
    ReturnAllToBase,
    EmergencyStop
}
