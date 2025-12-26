using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonFarmerSelections;

public class GetYearSeasonFarmerSelectionsQuery : IRequest<Result<YearSeasonFarmerSelectionsDto>>
{
    public Guid YearSeasonId { get; set; }
}

public class YearSeasonFarmerSelectionsDto
{
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public Guid? ClusterRiceVarietyId { get; set; }
    public string? ClusterRiceVarietyName { get; set; }
    public bool AllowFarmerSelection { get; set; }
    public DateTime? SelectionWindowStart { get; set; }
    public DateTime? SelectionWindowEnd { get; set; }
    public bool IsSelectionWindowOpen { get; set; }
    public FarmerSelectionStatus SelectionStatus { get; set; } = new();
}

public class FarmerSelectionStatus
{
    public int TotalFarmers { get; set; }
    public int FarmersWithSelection { get; set; }
    public int FarmersPending { get; set; }
    public decimal SelectionCompletionRate { get; set; }
    public List<VarietySelectionSummary> VarietySelections { get; set; } = new();
    public List<PendingFarmerInfo> PendingFarmers { get; set; } = new();
}

public class VarietySelectionSummary
{
    public Guid VarietyId { get; set; }
    public string VarietyName { get; set; } = string.Empty;
    public int SelectedByCount { get; set; }
    public int PreviousSeasonCount { get; set; }
    public bool IsRecommended { get; set; }
    public int NewSelections { get; set; }
    public int SwitchedIn { get; set; }
    public int SwitchedOut { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class PendingFarmerInfo
{
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PreviousVariety { get; set; }
    public int PlotCount { get; set; }
    public decimal? TotalArea { get; set; }
}

