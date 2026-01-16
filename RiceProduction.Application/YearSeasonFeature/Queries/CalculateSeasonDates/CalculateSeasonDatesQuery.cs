using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.CalculateSeasonDates;

/// <summary>
/// Query to calculate actual season dates based on Season's MM/DD format and a specific year
/// </summary>
public class CalculateSeasonDatesQuery : IRequest<Result<SeasonDatesDto>>
{
    /// <summary>
    /// Season ID to get the base dates from (MM/DD format)
    /// </summary>
    public Guid SeasonId { get; set; }
    
    /// <summary>
    /// Year to apply to the season dates
    /// </summary>
    public int Year { get; set; }
}

