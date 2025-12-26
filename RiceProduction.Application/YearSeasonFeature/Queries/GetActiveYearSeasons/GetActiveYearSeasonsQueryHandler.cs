using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetActiveYearSeasons;

public class GetActiveYearSeasonsQueryHandler : IRequestHandler<GetActiveYearSeasonsQuery, Result<ActiveYearSeasonsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetActiveYearSeasonsQueryHandler> _logger;

    public GetActiveYearSeasonsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetActiveYearSeasonsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ActiveYearSeasonsResponse>> Handle(
        GetActiveYearSeasonsQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Build query for active year seasons
            var query = _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Where(ys => ys.StartDate <= now && ys.EndDate >= now); // Active means current date is within start and end date

            // Apply optional filters
            if (request.ClusterId.HasValue)
            {
                query = query.Where(ys => ys.ClusterId == request.ClusterId.Value);
            }

            if (request.Year.HasValue)
            {
                query = query.Where(ys => ys.Year == request.Year.Value);
            }

            // Include related entities
            var yearSeasons = await query
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .Include(ys => ys.RiceVariety)
                .Include(ys => ys.ManagedByExpert)
                .Include(ys => ys.Groups)
                .OrderBy(ys => ys.StartDate)
                .ToListAsync(cancellationToken);

            var activeSeasons = yearSeasons.Select(ys => MapToDto(ys, now)).ToList();

            var response = new ActiveYearSeasonsResponse
            {
                ActiveSeasons = activeSeasons,
                TotalCount = activeSeasons.Count
            };

            _logger.LogInformation("Retrieved {Count} active year seasons", activeSeasons.Count);

            return Result<ActiveYearSeasonsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active year seasons");
            return Result<ActiveYearSeasonsResponse>.Failure("Failed to retrieve active year seasons");
        }
    }

    private ActiveYearSeasonDto MapToDto(YearSeason ys, DateTime now)
    {
        var daysUntilStart = (ys.StartDate - now).Days;
        var daysUntilEnd = (ys.EndDate - now).Days;
        var isInPlanningWindow = ys.PlanningWindowStart.HasValue && ys.PlanningWindowEnd.HasValue
            && now >= ys.PlanningWindowStart.Value && now <= ys.PlanningWindowEnd.Value;

        return new ActiveYearSeasonDto
        {
            Id = ys.Id,
            SeasonId = ys.SeasonId,
            SeasonName = ys.Season?.SeasonName ?? "Unknown",
            SeasonType = ys.Season?.SeasonType,
            ClusterId = ys.ClusterId,
            ClusterName = ys.Cluster?.ClusterName ?? "Unknown",
            Year = ys.Year,
            RiceVarietyId = ys.RiceVarietyId,
            RiceVarietyName = ys.RiceVariety?.VarietyName,
            StartDate = ys.StartDate,
            EndDate = ys.EndDate,
            BreakStartDate = ys.BreakStartDate,
            BreakEndDate = ys.BreakEndDate,
            PlanningWindowStart = ys.PlanningWindowStart,
            PlanningWindowEnd = ys.PlanningWindowEnd,
            Status = ys.Status.ToString(),
            Notes = ys.Notes,
            ManagedByExpertId = ys.ManagedByExpertId,
            ManagedByExpertName = ys.ManagedByExpert?.FullName,
            GroupCount = ys.Groups?.Count ?? 0,
            DaysUntilStart = daysUntilStart,
            DaysUntilEnd = daysUntilEnd,
            IsInPlanningWindow = isInPlanningWindow,
            DisplayName = $"{ys.Season?.SeasonName ?? "Unknown"} {ys.Year} - {ys.Cluster?.ClusterName ?? "Unknown"}"
        };
    }
}

