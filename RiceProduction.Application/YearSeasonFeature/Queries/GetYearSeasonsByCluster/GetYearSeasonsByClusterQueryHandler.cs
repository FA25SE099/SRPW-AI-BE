using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;

public class GetYearSeasonsByClusterQueryHandler : IRequestHandler<GetYearSeasonsByClusterQuery, Result<YearSeasonsByClusterResponse>>
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

    public async Task<Result<YearSeasonsByClusterResponse>> Handle(GetYearSeasonsByClusterQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get cluster
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<YearSeasonsByClusterResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var query = _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .AsNoTracking()
                .Where(ys => ys.ClusterId == request.ClusterId);

            if (request.Year.HasValue)
            {
                query = query.Where(ys => ys.Year == request.Year.Value);
            }

            var yearSeasons = await query
                .OrderByDescending(ys => ys.Year)
                .ThenBy(ys => ys.StartDate)
                .Select(ys => new
                {
                    ys.Id,
                    ys.SeasonId,
                    ys.Season.SeasonName,
                    ys.Season.SeasonType,
                    ys.ClusterId,
                    ys.Cluster.ClusterName,
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

            var now = DateTime.UtcNow;

            var allSeasons = yearSeasons.Select(ys => new YearSeasonDTO
            {
                Id = ys.Id,
                SeasonId = ys.SeasonId,
                SeasonName = ys.SeasonName,
                SeasonType = ys.SeasonType,
                ClusterId = ys.ClusterId,
                ClusterName = ys.ClusterName,
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
                IsCurrent = now >= ys.StartDate && now <= ys.EndDate,
                IsPast = now > ys.EndDate,
                IsUpcoming = now < ys.StartDate,
                DisplayName = $"{ys.SeasonName} {ys.Year}"
            }).ToList();

            var response = new YearSeasonsByClusterResponse
            {
                ClusterId = cluster.Id,
                ClusterName = cluster.ClusterName,
                CurrentSeason = allSeasons.FirstOrDefault(ys => ys.IsCurrent),
                PastSeasons = allSeasons.Where(ys => ys.IsPast).ToList(),
                UpcomingSeasons = allSeasons.Where(ys => ys.IsUpcoming).ToList(),
                AllSeasons = allSeasons
            };

            return Result<YearSeasonsByClusterResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving YearSeasons for cluster {ClusterId}", request.ClusterId);
            return Result<YearSeasonsByClusterResponse>.Failure("Failed to retrieve YearSeasons");
        }
    }
}

