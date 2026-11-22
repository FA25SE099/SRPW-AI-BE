using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetActiveYearSeasons;

public class GetActiveYearSeasonsQueryHandler : IRequestHandler<GetActiveYearSeasonsQuery, Result<ActiveYearSeasonsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetActiveYearSeasonsQueryHandler> _logger;

    public GetActiveYearSeasonsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetActiveYearSeasonsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ActiveYearSeasonsResponse>> Handle(
        GetActiveYearSeasonsQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Active means: Status = PlanningOpen AND AllowFarmerSelection = true 
            // AND current date is within FarmerSelectionWindow
            var query = _context.YearSeasons
                .AsNoTracking()
                .Where(ys => ys.Status == SeasonStatus.PlanningOpen
                    && ys.AllowFarmerSelection
                    && ys.FarmerSelectionWindowStart.HasValue
                    && ys.FarmerSelectionWindowEnd.HasValue
                    && now >= ys.FarmerSelectionWindowStart.Value
                    && now <= ys.FarmerSelectionWindowEnd.Value);

            if (request.ClusterId.HasValue)
            {
                query = query.Where(ys => ys.ClusterId == request.ClusterId.Value);
            }

            if (request.Year.HasValue)
            {
                query = query.Where(ys => ys.Year == request.Year.Value);
            }

            var yearSeasons = await query
                .OrderBy(ys => ys.StartDate)
                .Select(ys => new
                {
                    ys.Id,
                    ys.SeasonId,
                    SeasonName = ys.Season != null ? ys.Season.SeasonName : null,
                    SeasonType = ys.Season != null ? ys.Season.SeasonType : null,
                    ys.ClusterId,
                    ClusterName = ys.Cluster != null ? ys.Cluster.ClusterName : null,
                    ys.Year,
                    ys.RiceVarietyId,
                    RiceVarietyName = ys.RiceVariety != null ? ys.RiceVariety.VarietyName : null,
                    ys.StartDate,
                    ys.EndDate,
                    ys.BreakStartDate,
                    ys.BreakEndDate,
                    ys.PlanningWindowStart,
                    ys.PlanningWindowEnd,
                    ys.Status,
                    ys.Notes,
                    ys.ManagedByExpertId,
                    ManagedByExpertName = ys.ManagedByExpert != null ? ys.ManagedByExpert.FullName : null,
                    GroupCount = ys.Groups.Count
                })
                .ToListAsync(cancellationToken);

            var activeSeasons = yearSeasons.Select(ys =>
            {
                var seasonName = ys.SeasonName ?? "Unknown";
                var clusterName = ys.ClusterName ?? "Unknown";

                return new ActiveYearSeasonDto
                {
                    Id = ys.Id,
                    SeasonId = ys.SeasonId,
                    SeasonName = seasonName,
                    SeasonType = ys.SeasonType,
                    ClusterId = ys.ClusterId,
                    ClusterName = clusterName,
                    Year = ys.Year,
                    RiceVarietyId = ys.RiceVarietyId,
                    RiceVarietyName = ys.RiceVarietyName,
                    StartDate = ys.StartDate,
                    EndDate = ys.EndDate,
                    BreakStartDate = ys.BreakStartDate,
                    BreakEndDate = ys.BreakEndDate,
                    PlanningWindowStart = ys.PlanningWindowStart,
                    PlanningWindowEnd = ys.PlanningWindowEnd,
                    Status = ys.Status.ToString(),
                    Notes = ys.Notes,
                    ManagedByExpertId = ys.ManagedByExpertId,
                    ManagedByExpertName = ys.ManagedByExpertName,
                    GroupCount = ys.GroupCount,
                    DaysUntilStart = (ys.StartDate - now).Days,
                    DaysUntilEnd = (ys.EndDate - now).Days,
                    IsInPlanningWindow = ys.PlanningWindowStart.HasValue && ys.PlanningWindowEnd.HasValue
                        && now >= ys.PlanningWindowStart.Value && now <= ys.PlanningWindowEnd.Value,
                    DisplayName = $"{seasonName} {ys.Year} - {clusterName}"
                };
            }).ToList();

            var response = new ActiveYearSeasonsResponse
            {
                ActiveSeasons = activeSeasons,
                TotalCount = activeSeasons.Count
            };

            _logger.LogInformation("Retrieved {Count} active year seasons available for farmer selection", activeSeasons.Count);

            return Result<ActiveYearSeasonsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active year seasons");
            return Result<ActiveYearSeasonsResponse>.Failure("Failed to retrieve active year seasons");
        }
    }
}

