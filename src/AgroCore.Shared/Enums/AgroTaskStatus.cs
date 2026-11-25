namespace AgroCore.Shared.Enums;

/// <summary>
/// Estado de una tarea agrícola. Se llama AgroTaskStatus (y no TaskStatus) para no chocar con
/// System.Threading.Tasks.TaskStatus, que está importado implícitamente en todos los proyectos.
/// </summary>
public enum AgroTaskStatus
{
    /// <summary>Dependencias cumplidas: lista para asignarse a una máquina.</summary>
    Pending,
    /// <summary>Esperando dependencias (otra tarea, crecimiento, campo ocupado...).</summary>
    Waiting,
    Assigned,
    Running,
    Completed,
    Cancelled
}
