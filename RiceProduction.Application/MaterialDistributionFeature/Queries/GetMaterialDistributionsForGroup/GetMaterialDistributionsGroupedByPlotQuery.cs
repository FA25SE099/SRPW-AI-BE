using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

/// <summary>
/// Get material distributions grouped by plot/farmer for better UI display
/// Shows all materials for each farmer in a single card
/// </summary>
public class GetMaterialDistributionsGroupedByPlotQuery : IRequest<Result<GroupedMaterialDistributionsResponse>>
{
    public Guid GroupId { get; set; }
}

public class GroupedMaterialDistributionsResponse
{
    public Guid GroupId { get; set; }
    public int TotalFarmers { get; set; }
    public int TotalMaterials { get; set; }
    public List<FarmerMaterialDistribution> FarmerDistributions { get; set; } = new();
}

/// <summary>
/// All materials for a single farmer/plot
/// </summary>
public class FarmerMaterialDistribution
{
    public Guid PlotCultivationId { get; set; }
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    public string? Location { get; set; }
    
    /// <summary>
    /// Overall status: Pending if any pending, PartiallyConfirmed if all confirmed by supervisor, Completed if all confirmed by farmer
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// All materials for this farmer
    /// </summary>
    public List<MaterialItem> Materials { get; set; } = new();
    
    /// <summary>
    /// Distribution scheduling info (same for all materials)
    /// </summary>
    public DateTime ScheduledDistributionDate { get; set; }
    public DateTime DistributionDeadline { get; set; }
    public DateTime SupervisorConfirmationDeadline { get; set; }
    public DateTime? FarmerConfirmationDeadline { get; set; }
    
    /// <summary>
    /// Confirmation info (same for all materials since bulk confirmed)
    /// </summary>
    public Guid? SupervisorConfirmedBy { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime? SupervisorConfirmedAt { get; set; }
    public DateTime? ActualDistributionDate { get; set; }
    public string? SupervisorNotes { get; set; }
    public List<string>? ImageUrls { get; set; }
    
    /// <summary>
    /// Overdue flags
    /// </summary>
    public bool IsOverdue { get; set; }
    public bool IsSupervisorOverdue { get; set; }
    public bool IsFarmerOverdue { get; set; }
    
    /// <summary>
    /// Counts for UI badges
    /// </summary>
    public int TotalMaterialCount { get; set; }
    public int PendingMaterialCount { get; set; }
    public int ConfirmedMaterialCount { get; set; }
}

/// <summary>
/// Individual material in the distribution
/// </summary>
public class MaterialItem
{
    public Guid DistributionId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? FarmerConfirmedAt { get; set; }
    public string? FarmerNotes { get; set; }
}

