using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetActiveYearSeasons;

/// <summary>
/// Query to get all year seasons available for farmer cultivation selection.
/// Returns year seasons where Status=PlanningOpen, AllowFarmerSelection=true, 
/// and current date is within FarmerSelectionWindow.
/// </summary>
public class GetActiveYearSeasonsQuery : IRequest<Result<ActiveYearSeasonsResponse>>
{
    /// <summary>
    /// Optional filter by cluster ID
    /// </summary>
    public Guid? ClusterId { get; set; }
    
    /// <summary>
    /// Optional filter by year
    /// </summary>
    public int? Year { get; set; }
}

public class ActiveYearSeasonsResponse
{
    public List<ActiveYearSeasonDto> ActiveSeasons { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ActiveYearSeasonDto
{
    public Guid Id { get; set; }
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string? SeasonType { get; set; }
    public Guid ClusterId { get; set; }
    public string ClusterName { get; set; } = string.Empty;
    public int Year { get; set; }
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? BreakStartDate { get; set; }
    public DateTime? BreakEndDate { get; set; }
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? ManagedByExpertId { get; set; }
    public string? ManagedByExpertName { get; set; }
    public int GroupCount { get; set; }
    public int DaysUntilStart { get; set; }
    public int DaysUntilEnd { get; set; }
    public bool IsInPlanningWindow { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

