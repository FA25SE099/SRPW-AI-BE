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

            var now = DateTime.UtcNow;

            var allSeasons = yearSeasons.Select(ys => MapToDTO(ys, now)).ToList();

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

    private YearSeasonDTO MapToDTO(YearSeason ys, DateTime now)
    {
        var isCurrent = now >= ys.StartDate && now <= ys.EndDate;
        var isPast = now > ys.EndDate;
        var isUpcoming = now < ys.StartDate;

        return new YearSeasonDTO
        {
            Id = ys.Id,
            SeasonId = ys.SeasonId,
            SeasonName = ys.Season.SeasonName,
            SeasonType = ys.Season.SeasonType,
            ClusterId = ys.ClusterId,
            ClusterName = ys.Cluster.ClusterName,
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
            GroupCount = ys.Groups.Count,
            IsCurrent = isCurrent,
            IsPast = isPast,
            IsUpcoming = isUpcoming,
            DisplayName = $"{ys.Season.SeasonName} {ys.Year}"
        };
    }
}

