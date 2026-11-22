using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterCurrentSeason;

public class GetClusterCurrentSeasonQueryHandler
    : IRequestHandler<GetClusterCurrentSeasonQuery, Result<ClusterCurrentSeasonResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetClusterCurrentSeasonQueryHandler> _logger;

    public GetClusterCurrentSeasonQueryHandler(
        IApplicationDbContext context,
        ILogger<GetClusterCurrentSeasonQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ClusterCurrentSeasonResponse>> Handle(
        GetClusterCurrentSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _context.Clusters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (cluster == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var allSeasons = await _context.Seasons
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var (currentSeason, currentYear) = ResolveCurrentSeasonAndYear(allSeasons);
            if (currentSeason == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    "No current season could be determined");
            }

            var groupsList = await _context.Groups
                .AsNoTracking()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys!.RiceVariety)
                .Where(g => g.ClusterId == cluster.Id &&
                           g.YearSeason != null && g.YearSeason.SeasonId == currentSeason.Id &&
                           g.Year == currentYear)
                .ToListAsync(cancellationToken);
            bool hasGroups = groupsList.Any();

            var response = new ClusterCurrentSeasonResponse
            {
                ClusterId = cluster.Id,
                ClusterName = cluster.ClusterName,
                CurrentSeason = new CurrentSeasonInfo
                {
                    SeasonId = currentSeason.Id,
                    SeasonName = currentSeason.SeasonName,
                    SeasonType = currentSeason.SeasonType ?? "",
                    Year = currentYear,
                    IsCurrent = true
                },
                HasGroups = hasGroups
            };

            List<Farmer>? clusterFarmers = null;
            List<Plot>? clusterPlots = null;

            if (hasGroups)
            {
                await PopulateGroupDetailsAsync(response, groupsList, cancellationToken);
            }
            else
            {
                (clusterFarmers, clusterPlots) = await GetClusterFarmersAndPlotsAsync(cluster.Id, cancellationToken);
                await PopulateReadinessInfoAsync(response, cluster.Id, clusterFarmers, clusterPlots, cancellationToken);
            }

            if (clusterFarmers == null || clusterPlots == null)
            {
                (clusterFarmers, clusterPlots) = await GetClusterFarmersAndPlotsAsync(cluster.Id, cancellationToken);
            }

            await PopulateRiceVarietySelectionAsync(
                response,
                currentSeason.Id,
                allSeasons,
                clusterFarmers,
                clusterPlots,
                cancellationToken);

            return Result<ClusterCurrentSeasonResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving current season status for cluster {ClusterId}",
                request.ClusterId);
            return Result<ClusterCurrentSeasonResponse>.Failure(
                $"Error retrieving current season status: {ex.Message}");
        }
    }

    private async Task<(List<Farmer> Farmers, List<Plot> Plots)> GetClusterFarmersAndPlotsAsync(
        Guid clusterId,
        CancellationToken cancellationToken)
    {
        var farmers = await _context.Farmers
            .AsNoTracking()
            .Where(f => f.ClusterId == clusterId)
            .ToListAsync(cancellationToken);
        var farmerIds = farmers.Select(f => f.Id).ToList();

        var plots = await _context.Plots
            .AsNoTracking()
            .Where(p => farmerIds.Contains(p.FarmerId))
            .ToListAsync(cancellationToken);

        return (farmers, plots);
    }

    private async Task PopulateGroupDetailsAsync(
        ClusterCurrentSeasonResponse response,
        List<Group> groups,
        CancellationToken cancellationToken)
    {
        var groupIds = groups.Select(g => g.Id).ToList();

        var groupPlots = await _context.GroupPlots
            .AsNoTracking()
            .Where(gp => groupIds.Contains(gp.GroupId))
            .Select(gp => new { gp.GroupId, gp.Plot.FarmerId })
            .ToListAsync(cancellationToken);

        var farmerIds = groupPlots.Select(gp => gp.FarmerId).Distinct().ToList();
        var totalFarmers = await _context.Farmers
            .AsNoTracking()
            .CountAsync(f => farmerIds.Contains(f.Id), cancellationToken);

        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();

        var supervisorDict = await _context.Supervisors
            .AsNoTracking()
            .Where(s => supervisorIds.Contains(s.Id) && s.ClusterId == response.ClusterId)
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        response.Groups = groups.Select(g => new GroupSeasonSummary
        {
            GroupId = g.Id,
            SupervisorId = g.SupervisorId,
            SupervisorName = g.SupervisorId.HasValue
                ? supervisorDict.GetValueOrDefault(g.SupervisorId.Value)
                : null,
            RiceVarietyId = g.YearSeason?.RiceVarietyId,
            RiceVarietyName = g.YearSeason?.RiceVariety?.VarietyName,
            PlantingDate = g.PlantingDate,
            Status = g.Status.ToString(),
            PlotCount = groupPlots.Count(gp => gp.GroupId == g.Id),
            TotalArea = g.TotalArea
        }).ToList();

        response.RiceVarietyBreakdown = groups
            .Where(g => g.YearSeason?.RiceVarietyId != null)
            .GroupBy(g => g.YearSeason!.RiceVarietyId!.Value)
            .Select(g =>
            {
                var varietyGroupIds = g.Select(gr => gr.Id).ToHashSet();
                return new RiceVarietyGroupSummary
                {
                    RiceVarietyId = g.Key,
                    RiceVarietyName = g.First().YearSeason?.RiceVariety?.VarietyName ?? "Unknown",
                    GroupCount = g.Count(),
                    PlotCount = groupPlots.Count(gp => varietyGroupIds.Contains(gp.GroupId)),
                    TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
                };
            })
            .OrderByDescending(v => v.PlotCount)
            .ToList();

        response.TotalPlots = groupPlots.Count;
        response.TotalFarmers = totalFarmers;
        response.TotalArea = groups.Sum(g => g.TotalArea ?? 0);
    }

    private async Task PopulateReadinessInfoAsync(
        ClusterCurrentSeasonResponse response,
        Guid clusterId,
        List<Farmer> farmersList,
        List<Plot> plotsList,
        CancellationToken cancellationToken)
    {
        var availableSupervisorCount = await _context.Supervisors
            .AsNoTracking()
            .CountAsync(s => s.IsActive && s.ClusterId == clusterId, cancellationToken);

        var plotsWithPolygon = plotsList.Count(p => p.Boundary != null && p.Status == PlotStatus.Active);
        var plotsWithoutPolygon = plotsList.Count(p => p.Boundary == null || p.Status == PlotStatus.PendingPolygon);

        var blockingIssues = new List<string>();
        var recommendations = new List<string>();

        if (plotsWithoutPolygon > 0)
        {
            blockingIssues.Add($"{plotsWithoutPolygon} plots missing polygon boundaries");
            recommendations.Add("Assign polygon drawing tasks to supervisors");
        }

        if (availableSupervisorCount == 0)
        {
            blockingIssues.Add("No supervisors available with capacity");
            recommendations.Add("Add more supervisors or increase capacity");
        }

        if (farmersList.Count < 5)
        {
            blockingIssues.Add($"Insufficient farmers (need at least 5, have {farmersList.Count})");
            recommendations.Add("Import more farmers to the cluster");
        }

        var readinessScore = 0;
        if (plotsWithPolygon > 0) readinessScore += 40;
        if (availableSupervisorCount > 0) readinessScore += 30;
        if (farmersList.Count >= 5) readinessScore += 30;

        response.Readiness = new ClusterReadinessInfo
        {
            IsReadyToFormGroups = !blockingIssues.Any(),
            AvailablePlots = plotsList.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plotsWithoutPolygon,
            AvailableSupervisors = availableSupervisorCount,
            AvailableFarmers = farmersList.Count,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations,
            ReadinessScore = readinessScore
        };
    }

    private async Task PopulateRiceVarietySelectionAsync(
        ClusterCurrentSeasonResponse response,
        Guid seasonId,
        List<Season> allSeasons,
        List<Farmer> farmersList,
        List<Plot> plotsList,
        CancellationToken cancellationToken)
    {
        var farmerIds = farmersList.Select(f => f.Id).ToList();
        var totalFarmers = farmersList.Count;
        var plotIds = plotsList.Select(p => p.Id).ToList();
        var plotToFarmerId = plotsList.ToDictionary(p => p.Id, p => p.FarmerId);

        var cultivationsList = await _context.PlotCultivations
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId)
            .ToListAsync(cancellationToken);

        var farmersWithSelection = cultivationsList
            .Select(pc => plotToFarmerId[pc.PlotId])
            .Distinct()
            .ToList();

        var farmersPending = totalFarmers - farmersWithSelection.Count;
        var completionRate = totalFarmers > 0 ? (decimal)farmersWithSelection.Count / totalFarmers * 100 : 0;

        var previousSeason = allSeasons
            .OrderByDescending(s => s.CreatedAt)
            .ElementAtOrDefault(1);

        var previousCultivations = previousSeason != null
            ? await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == previousSeason.Id)
                .ToListAsync(cancellationToken)
            : new List<PlotCultivation>();

        var varietyIds = cultivationsList.Select(pc => pc.RiceVarietyId).Distinct().ToList();
        var varietyDict = await _context.RiceVarieties
            .AsNoTracking()
            .Where(rv => varietyIds.Contains(rv.Id))
            .ToDictionaryAsync(rv => rv.Id, rv => rv.VarietyName, cancellationToken);

        var selections = cultivationsList
            .GroupBy(pc => pc.RiceVarietyId)
            .Select(g =>
            {
                var varietyId = g.Key;
                var previousCount = previousCultivations.Count(pc => pc.RiceVarietyId == varietyId);
                var currentCount = g.Count();

                return new VarietySelectionSummary
                {
                    VarietyId = varietyId,
                    VarietyName = varietyDict.GetValueOrDefault(varietyId) ?? "Unknown",
                    SelectedBy = currentCount,
                    PreviousSeason = previousCount,
                    IsRecommended = true,
                    NewSelections = 0,
                    SwitchedIn = Math.Max(0, currentCount - previousCount),
                    SwitchedOut = Math.Max(0, previousCount - currentCount)
                };
            })
            .OrderByDescending(v => v.SelectedBy)
            .ToList();

        var farmerDict = farmersList.ToDictionary(f => f.Id);
        var plotsByFarmerId = plotsList.ToLookup(p => p.FarmerId);
        var previousCultivationsByFarmerId = previousCultivations
            .Where(pc => plotToFarmerId.ContainsKey(pc.PlotId))
            .ToLookup(pc => plotToFarmerId[pc.PlotId]);

        var pendingFarmerIds = farmerIds.Except(farmersWithSelection).ToList();
        var pendingFarmers = pendingFarmerIds.Select(fid => new PendingFarmerInfo
        {
            FarmerId = fid,
            FarmerName = farmerDict.GetValueOrDefault(fid)?.FullName ?? "Unknown",
            PreviousVariety = previousCultivationsByFarmerId[fid]
                .Select(pc => varietyDict.GetValueOrDefault(pc.RiceVarietyId))
                .FirstOrDefault(),
            PlotCount = plotsByFarmerId[fid].Count()
        }).ToList();

        response.RiceVarietySelection = new RiceVarietySelectionStatus
        {
            TotalFarmers = totalFarmers,
            FarmersWithSelection = farmersWithSelection.Count,
            FarmersPending = farmersPending,
            SelectionCompletionRate = completionRate,
            Selections = selections,
            PendingFarmers = pendingFarmers
        };
    }

    private static (Season? season, int year) ResolveCurrentSeasonAndYear(IEnumerable<Season> allSeasons)
    {
        var today = DateTime.Now;

        foreach (var season in allSeasons)
        {
            if (!SeasonDateHelper.IsDateInSeasonRange(today.Month, today.Day, season.StartDate, season.EndDate))
            {
                continue;
            }

            SeasonDateHelper.TryParseMonthDay(season.StartDate, out var startMonth, out _);
            int year = today.Month < startMonth && startMonth > 6 ? today.Year - 1 : today.Year;

            return (season, year);
        }

        return (null, today.Year);
    }
}

