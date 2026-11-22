using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;

public class GetClusterHistoryQueryHandler
    : IRequestHandler<GetClusterHistoryQuery, Result<ClusterHistoryResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetClusterHistoryQueryHandler> _logger;

    public GetClusterHistoryQueryHandler(
        IApplicationDbContext context,
        ILogger<GetClusterHistoryQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ClusterHistoryResponse>> Handle(
        GetClusterHistoryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _context.Clusters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (cluster == null)
            {
                return Result<ClusterHistoryResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var allSeasons = await _context.Seasons
                .AsNoTracking()
                .ToListAsync(cancellationToken);

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

        var groups = await _context.Groups
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
        var groupPlots = await _context.GroupPlots
            .AsNoTracking()
            .Where(gp => groupIds.Contains(gp.GroupId))
            .Select(gp => new { gp.GroupId, gp.PlotId, gp.Plot.FarmerId })
            .ToListAsync(cancellationToken);

        var farmerIds = groupPlots.Select(gp => gp.FarmerId).Distinct().ToList();
        var totalFarmers = await _context.Farmers
            .AsNoTracking()
            .CountAsync(f => farmerIds.Contains(f.Id), cancellationToken);

        var plotIds = groupPlots.Select(gp => gp.PlotId).ToList();
        var plotCultivations = await _context.PlotCultivations
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == season.Id)
            .Select(pc => new { pc.ActualYield, pc.Area })
            .ToListAsync(cancellationToken);

        var varietyIds = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .Select(g => g.RiceVarietyId!.Value)
            .Distinct()
            .ToList();

        var varietyDict = await _context.RiceVarieties
            .AsNoTracking()
            .Where(rv => varietyIds.Contains(rv.Id))
            .ToDictionaryAsync(rv => rv.Id, rv => rv.VarietyName, cancellationToken);

        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();

        var supervisorDict = await _context.Supervisors
            .AsNoTracking()
            .Where(s => supervisorIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var completedProductionPlans = await _context.ProductionPlans
            .AsNoTracking()
            .CountAsync(
                pp => pp.GroupId.HasValue &&
                      groupIds.Contains(pp.GroupId.Value) &&
                      pp.Status == Domain.Enums.TaskStatus.Completed,
                cancellationToken);

        var varietyBreakdown = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .GroupBy(g => g.RiceVarietyId!.Value)
            .Select(g =>
            {
                var varietyGroupIds = g.Select(gr => gr.Id).ToHashSet();
                return new RiceVarietyGroupSummary
                {
                    RiceVarietyId = g.Key,
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.Key) ?? "Unknown",
                    GroupCount = g.Count(),
                    PlotCount = groupPlots.Count(gp => varietyGroupIds.Contains(gp.GroupId)),
                    TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
                };
            })
            .OrderByDescending(v => v.PlotCount)
            .ToList();

        var averageYield = plotCultivations
            .Where(pc => pc.ActualYield.HasValue)
            .Average(pc => pc.ActualYield);

        var totalProduction = plotCultivations
            .Where(pc => pc.ActualYield.HasValue && pc.Area.HasValue)
            .Sum(pc => pc.ActualYield!.Value * pc.Area!.Value);

        var groupSummaries = groups.Select(g => new GroupSeasonSummary
        {
            GroupId = g.Id,
            SupervisorId = g.SupervisorId,
            SupervisorName = g.SupervisorId.HasValue
                ? supervisorDict.GetValueOrDefault(g.SupervisorId.Value)
                : null,
            RiceVarietyId = g.RiceVarietyId,
            RiceVarietyName = g.RiceVarietyId.HasValue
                ? varietyDict.GetValueOrDefault(g.RiceVarietyId.Value)
                : null,
            PlantingDate = g.PlantingDate,
            Status = g.Status.ToString(),
            PlotCount = groupPlots.Count(gp => gp.GroupId == g.Id),
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
            TotalPlots = groupPlots.Count,
            TotalFarmers = totalFarmers,
            TotalArea = groups.Sum(g => g.TotalArea ?? 0),
            RiceVarietyBreakdown = varietyBreakdown,
            AverageYield = averageYield,
            TotalProduction = totalProduction,
            CompletedProductionPlans = completedProductionPlans,
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

