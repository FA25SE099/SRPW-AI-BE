namespace RiceProduction.Domain.Enums;

public enum DistributionStatus
{
    Pending = 0,              // Not yet distributed
    PartiallyConfirmed = 1,   // Either supervisor OR farmer confirmed (but not both)
    Completed = 2,            // Both supervisor AND farmer confirmed
    Rejected = 3              // Rejected by either party
}

