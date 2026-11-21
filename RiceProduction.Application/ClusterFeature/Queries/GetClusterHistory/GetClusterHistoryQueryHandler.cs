using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;

public class GetClusterHistoryQueryHandler
    : IRequestHandler<GetClusterHistoryQuery, Result<ClusterHistoryResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterHistoryQueryHandler> _logger;

    public GetClusterHistoryQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterHistoryQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ClusterHistoryResponse>> Handle(
        GetClusterHistoryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<ClusterHistoryResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);

            var targetSeasons = allSeasons.AsEnumerable();

            if (request.SeasonId.HasValue)
            {
                targetSeasons = targetSeasons.Where(s => s.Id == request.SeasonId.Value);
            }

            if (request.Year.HasValue)
            {
                targetSeasons = targetSeasons.Where(s =>
                    GetSeasonYear(s, request.Year.Value) == request.Year.Value);
            }

            var selectedSeasons = targetSeasons
                .OrderByDescending(s => s.CreatedAt)
                .Take(request.Limit ?? 5)
                .ToList();

            var seasonSnapshots = new List<ClusterSeasonSnapshot>(selectedSeasons.Count);

            foreach (var season in selectedSeasons)
            {
                seasonSnapshots.Add(await BuildSeasonSnapshotAsync(cluster, season, cancellationToken));
            }

            _logger.LogInformation(
                "Retrieved {SnapshotCount} season snapshots for cluster {ClusterId}",
                seasonSnapshots.Count, cluster.Id);

            return Result<ClusterHistoryResponse>.Success(new ClusterHistoryResponse
            {
                ClusterId = cluster.Id,
                ClusterName = cluster.ClusterName,
                SeasonSnapshots = seasonSnapshots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving cluster history for cluster {ClusterId}",
                request.ClusterId);
            return Result<ClusterHistoryResponse>.Failure(
                $"Error retrieving cluster history: {ex.Message}");
        }
    }

    private async Task<ClusterSeasonSnapshot> BuildSeasonSnapshotAsync(
        Cluster cluster,
        Season season,
        CancellationToken cancellationToken)
    {
        var year = GetSeasonYear(season, DateTime.Now.Year);

        var groups = await _unitOfWork.Repository<Group>()
            .GetQueryable()
            .AsNoTracking()
            .Where(g => g.ClusterId == cluster.Id &&
                        g.YearSeason != null && g.YearSeason.SeasonId == season.Id &&
                        g.Year == year)
            .Select(g => new
            {
                g.Id,
                g.SupervisorId,
                g.PlantingDate,
                g.Status,
                g.TotalArea,
                RiceVarietyId = g.YearSeason!.RiceVarietyId
            })
            .ToListAsync(cancellationToken);

        if (!groups.Any())
        {
            return new ClusterSeasonSnapshot
            {
                SeasonId = season.Id,
                SeasonName = season.SeasonName,
                SeasonType = season.SeasonType ?? "",
                Year = year,
                StartDate = season.StartDate,
                EndDate = season.EndDate,
                IsActive = season.IsActive,
                TotalGroups = 0,
                TotalPlots = 0,
                TotalFarmers = 0,
                TotalArea = 0
            };
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var groupPlots = await _unitOfWork.Repository<GroupPlot>()
            .ListAsync(gp => groupIds.Contains(gp.GroupId),
                includeProperties: q => q.Include(gp => gp.Plot));
        var plotsList = groupPlots.Select(gp => gp.Plot).ToList();

        var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => farmerIds.Contains(f.Id));

        var plotIds = plotsList.Select(p => p.Id).ToList();
        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == season.Id);

        var varietyIds = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .Select(g => g.RiceVarietyId!.Value)
            .Distinct()
            .ToList();

        var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
            .ListAsync(rv => varietyIds.Contains(rv.Id));
        var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();

        var supervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => supervisorIds.Contains(s.Id));
        var supervisorDict = supervisors.ToDictionary(s => s.Id);

        var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
            .ListAsync(pp => pp.GroupId.HasValue && groupIds.Contains(pp.GroupId.Value));

        var varietyBreakdown = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .GroupBy(g => g.RiceVarietyId!.Value)
            .Select(g =>
            {
                var varietyGroupIds = g.Select(gr => gr.Id).ToHashSet();
                return new RiceVarietyGroupSummary
                {
                    RiceVarietyId = g.Key,
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.Key)?.VarietyName ?? "Unknown",
                    GroupCount = g.Count(),
                    PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => varietyGroupIds.Contains(gp.GroupId))),
                    TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
                };
            })
            .OrderByDescending(v => v.PlotCount)
            .ToList();

        var averageYield = plotCultivations
            .Where(pc => pc.ActualYield.HasValue)
            .Average(pc => (decimal?)pc.ActualYield);

        var totalProduction = plotCultivations
            .Where(pc => pc.ActualYield.HasValue && pc.Area.HasValue)
            .Sum(pc => pc.ActualYield!.Value * pc.Area!.Value);

        var groupSummaries = groups.Select(g => new GroupSeasonSummary
        {
            GroupId = g.Id,
            SupervisorId = g.SupervisorId,
            SupervisorName = g.SupervisorId.HasValue
                ? supervisorDict.GetValueOrDefault(g.SupervisorId.Value)?.FullName
                : null,
            RiceVarietyId = g.RiceVarietyId,
            RiceVarietyName = g.RiceVarietyId.HasValue
                ? varietyDict.GetValueOrDefault(g.RiceVarietyId.Value)?.VarietyName
                : null,
            PlantingDate = g.PlantingDate,
            Status = g.Status.ToString(),
            PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => gp.GroupId == g.Id)),
            TotalArea = g.TotalArea
        }).ToList();

        return new ClusterSeasonSnapshot
        {
            SeasonId = season.Id,
            SeasonName = season.SeasonName,
            SeasonType = season.SeasonType ?? "",
            Year = year,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            IsActive = season.IsActive,
            TotalGroups = groups.Count,
            TotalPlots = plotsList.Count,
            TotalFarmers = farmers.Count(),
            TotalArea = groups.Sum(g => g.TotalArea ?? 0),
            RiceVarietyBreakdown = varietyBreakdown,
            AverageYield = averageYield,
            TotalProduction = totalProduction,
            CompletedProductionPlans = productionPlans.Count(pp => pp.Status == Domain.Enums.TaskStatus.Completed),
            Groups = groupSummaries
        };
    }

    private static int GetSeasonYear(Season season, int currentYear)
    {
        if (!SeasonDateHelper.TryParseMonthDay(season.StartDate, out var startMonth, out _))
        {
            return currentYear;
        }

        // Seasons starting Oct-Dec span into the following calendar year
        return startMonth >= 10 && DateTime.Now.Month < startMonth
            ? currentYear - 1
            : currentYear;
    }
}

