using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByYearSeason;

/// <summary>
/// Query to get plots with their season-specific cultivation information
/// </summary>
public class GetPlotsByYearSeasonQuery : IRequest<PagedResult<IEnumerable<PlotWithSeasonInfoDto>>>
{
    public Guid YearSeasonId { get; set; }
    public Guid? ClusterManagerId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
    
    // Optional filters
    public bool? HasMadeSelection { get; set; }
    public bool? IsInGroup { get; set; }
    public Guid? GroupId { get; set; }
}

