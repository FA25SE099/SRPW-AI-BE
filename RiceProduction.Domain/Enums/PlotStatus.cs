namespace RiceProduction.Domain.Enums;

public enum PlotStatus
{
    Active,
    Inactive,
    Emergency,
    Locked,
    PendingPolygon  // Plot created but polygon boundary not yet assigned
}