namespace AgroCore.Shared.Enums;

public enum TaskType
{
    Sow,
    /// <summary>Tarea interna del servidor: el crecimiento del cultivo (no la ejecuta ninguna máquina).</summary>
    Growth,
    Irrigate,
    Spray,
    Harvest
}
