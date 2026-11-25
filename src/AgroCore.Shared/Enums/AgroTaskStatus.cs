namespace AgroCore.Shared.Enums;

/// <summary>
/// Nekazaritza-zeregin baten egoera. AgroTaskStatus deitzen da (eta ez TaskStatus) talkarik ez izateko
/// System.Threading.Tasks.TaskStatus-ekin, proiektu guztietan inplizituki inportatuta baitago.
/// </summary>
public enum AgroTaskStatus
{
    /// <summary>Mendekotasunak beteta: makina bati esleitzeko prest.</summary>
    Pending,
    /// <summary>Mendekotasunen zain (beste zeregin bat, hazkuntza, soro okupatua...).</summary>
    Waiting,
    Assigned,
    Running,
    Completed,
    Cancelled
}
