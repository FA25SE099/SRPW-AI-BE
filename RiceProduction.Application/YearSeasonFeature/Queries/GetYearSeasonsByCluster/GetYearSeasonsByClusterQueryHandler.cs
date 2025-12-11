using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;

public class GetYearSeasonsByClusterQueryHandler : IRequestHandler<GetYearSeasonsByClusterQuery, Result<List<YearSeasonDTO>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetYearSeasonsByClusterQueryHandler> _logger;

    public GetYearSeasonsByClusterQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetYearSeasonsByClusterQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<YearSeasonDTO>>> Handle(GetYearSeasonsByClusterQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Where(ys => ys.ClusterId == request.ClusterId);

            if (request.Year.HasValue)
            {
                query = query.Where(ys => ys.Year == request.Year.Value);
            }

            var yearSeasons = await query
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .Include(ys => ys.RiceVariety)
                .Include(ys => ys.ManagedByExpert)
                .Include(ys => ys.Groups)
                .OrderByDescending(ys => ys.Year)
                .ThenBy(ys => ys.StartDate)
                .ToListAsync(cancellationToken);

            var result = yearSeasons.Select(ys => new YearSeasonDTO
            {
                Id = ys.Id,
                SeasonId = ys.SeasonId,
                SeasonName = ys.Season.SeasonName,
                SeasonType = ys.Season.SeasonType,
                ClusterId = ys.ClusterId,
                ClusterName = ys.Cluster.ClusterName,
                Year = ys.Year,
                RiceVarietyId = ys.RiceVarietyId,
                RiceVarietyName = ys.RiceVariety.VarietyName,
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
                GroupCount = ys.Groups.Count
            }).ToList();

            return Result<List<YearSeasonDTO>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving YearSeasons for cluster {ClusterId}", request.ClusterId);
            return Result<List<YearSeasonDTO>>.Failure("Failed to retrieve YearSeasons");
        }
    }
}

