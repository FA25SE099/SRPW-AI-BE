using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorAvailableSeasons;

public class GetSupervisorAvailableSeasonsQuery : IRequest<Result<List<AvailableSeasonYearDto>>>
{
    public Guid SupervisorId { get; set; }
}

/// <summary>
/// Season + Year combination for selector dropdown
/// </summary>
public class AvailableSeasonYearDto
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public int Year { get; set; }
    public string DisplayName { get; set; } = string.Empty; // "Đông Xuân 2024"
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsPast { get; set; }
    public bool HasGroup { get; set; }
    
    // Optional: Group summary
    public Guid? GroupId { get; set; }
    public string? GroupStatus { get; set; }
    public bool? HasProductionPlan { get; set; }
    public int? PlotCount { get; set; }
}

