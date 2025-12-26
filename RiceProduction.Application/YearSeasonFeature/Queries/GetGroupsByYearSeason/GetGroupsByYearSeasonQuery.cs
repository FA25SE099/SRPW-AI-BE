using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetGroupsByYearSeason;

/// <summary>
/// Query to get all groups for a specific YearSeason
/// </summary>
public class GetGroupsByYearSeasonQuery : IRequest<Result<GetGroupsByYearSeasonResponse>>
{
    /// <summary>
    /// The YearSeason ID to get groups for
    /// </summary>
    public Guid YearSeasonId { get; set; }
}

