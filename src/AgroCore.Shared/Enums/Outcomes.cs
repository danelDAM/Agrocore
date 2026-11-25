namespace AgroCore.Shared.Enums;

/// <summary>Makina baten zeregin-eskaeraren (TASK_REQUEST) emaitza.</summary>
public enum AssignmentOutcome
{
    Assigned,
    /// <summary>Ez dago zereginik makinak adierazitako itxaronaldian.</summary>
    NoTask,
    /// <summary>Zereginak badaude, baina bateria ez da nahikoa haiek egin eta itzultzeko: kargatu behar du.</summary>
    NeedsCharge,
    /// <summary>Itxaronaldia bertan behera utzi zen (STOP, larrialdia, basera itzultzea...).</summary>
    Cancelled
}

public enum ZoneOutcome
{
    Granted,
    /// <summary>Ukatua elkarblokeo bat saihesteko (itxaronaldien zikloa). Makinak bidea utzi behar du.</summary>
    DeniedDeadlock,
    Cancelled
}

public enum ChargerOutcome
{
    Granted,
    Cancelled
}
