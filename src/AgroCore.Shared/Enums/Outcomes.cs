namespace AgroCore.Shared.Enums;

/// <summary>Resultado de una petición de tarea (TASK_REQUEST) de una máquina.</summary>
public enum AssignmentOutcome
{
    Assigned,
    /// <summary>No hay tarea en el tiempo de espera indicado por la máquina.</summary>
    NoTask,
    /// <summary>Existen tareas, pero la batería no alcanza para hacerlas y volver: debe cargar.</summary>
    NeedsCharge,
    /// <summary>La espera fue cancelada (STOP, emergencia, retorno a base...).</summary>
    Cancelled
}

public enum ZoneOutcome
{
    Granted,
    /// <summary>Denegada para evitar un interbloqueo (ciclo de esperas). La máquina debe ceder el paso.</summary>
    DeniedDeadlock,
    Cancelled
}

public enum ChargerOutcome
{
    Granted,
    Cancelled
}
