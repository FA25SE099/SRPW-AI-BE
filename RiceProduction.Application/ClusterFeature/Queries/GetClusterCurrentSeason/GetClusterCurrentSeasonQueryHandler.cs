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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterCurrentSeasonQueryHandler> _logger;

    public GetClusterCurrentSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterCurrentSeasonQueryHandler> logger
        )
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ClusterCurrentSeasonResponse>> Handle(
        GetClusterCurrentSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var (currentSeason, currentYear) = await GetCurrentSeasonAndYear();
            if (currentSeason == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    "No current season could be determined");
            }

            var groupsList = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.RiceVariety)
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
                await PopulateGroupDetailsAsync(response, groupsList);
            }
            else
            {
                (clusterFarmers, clusterPlots) = await GetClusterFarmersAndPlotsAsync(cluster.Id);
                await PopulateReadinessInfoAsync(response, cluster.Id, clusterFarmers, clusterPlots);
            }

            if (clusterFarmers == null || clusterPlots == null)
            {
                (clusterFarmers, clusterPlots) = await GetClusterFarmersAndPlotsAsync(cluster.Id);
            }

            await PopulateRiceVarietySelectionAsync(response, currentSeason.Id, clusterFarmers, clusterPlots);

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

    private async Task<(List<Farmer> Farmers, List<Plot> Plots)> GetClusterFarmersAndPlotsAsync(Guid clusterId)
    {
        var farmers = (await _unitOfWork.FarmerRepository
            .ListAsync(f => f.ClusterId == clusterId)).ToList();
        var farmerIds = farmers.Select(f => f.Id).ToList();

        var plots = (await _unitOfWork.Repository<Plot>()
            .ListAsync(p => farmerIds.Contains(p.FarmerId))).ToList();

        return (farmers, plots);
    }

    private async Task PopulateGroupDetailsAsync(
        ClusterCurrentSeasonResponse response,
        List<Group> groups)
    {
        var groupIds = groups.Select(g => g.Id).ToList();

        var groupPlots = await _unitOfWork.Repository<GroupPlot>()
            .ListAsync(gp => groupIds.Contains(gp.GroupId),
                includeProperties: q => q.Include(gp => gp.Plot));
        var plotsList = groupPlots.Select(gp => gp.Plot).ToList();

        var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => farmerIds.Contains(f.Id));

        var varietyIds = groups
            .Where(g => g.YearSeason?.RiceVarietyId != null)
            .Select(g => g.YearSeason!.RiceVarietyId)
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
            .ListAsync(s => supervisorIds.Contains(s.Id) && s.ClusterId == response.ClusterId);
        var supervisorDict = supervisors.ToDictionary(s => s.Id);

        response.Groups = groups.Select(g => new GroupSeasonSummary
        {
            GroupId = g.Id,
            SupervisorId = g.SupervisorId,
            SupervisorName = g.SupervisorId.HasValue
                ? supervisorDict.GetValueOrDefault(g.SupervisorId.Value)?.FullName
                : null,
            RiceVarietyId = g.YearSeason?.RiceVarietyId,
            RiceVarietyName = g.YearSeason?.RiceVarietyId != null
                ? varietyDict.GetValueOrDefault(g.YearSeason.RiceVarietyId.Value)?.VarietyName
                : null,
            PlantingDate = g.PlantingDate,
            Status = g.Status.ToString(),
            PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => gp.GroupId == g.Id)),
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
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.Key)?.VarietyName ?? "Unknown",
                    GroupCount = g.Count(),
                    PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => varietyGroupIds.Contains(gp.GroupId))),
                    TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
                };
            })
            .OrderByDescending(v => v.PlotCount)
            .ToList();

        response.TotalPlots = plotsList.Count;
        response.TotalFarmers = farmers.Count();
        response.TotalArea = groups.Sum(g => g.TotalArea ?? 0);
    }

    private async Task PopulateReadinessInfoAsync(
        ClusterCurrentSeasonResponse response,
        Guid clusterId,
        List<Farmer> farmersList,
        List<Plot> plotsList)
    {
        var availableSupervisors = (await _unitOfWork.SupervisorRepository
            .ListAsync(s => s.IsActive && s.ClusterId == clusterId)).ToList();

        var plotsWithPolygon = plotsList.Count(p => p.Boundary != null && p.Status == PlotStatus.Active);
        var plotsWithoutPolygon = plotsList.Count(p => p.Boundary == null || p.Status == PlotStatus.PendingPolygon);

        var blockingIssues = new List<string>();
        var recommendations = new List<string>();

        if (plotsWithoutPolygon > 0)
        {
            blockingIssues.Add($"{plotsWithoutPolygon} plots missing polygon boundaries");
            recommendations.Add("Assign polygon drawing tasks to supervisors");
        }

        if (!availableSupervisors.Any())
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
        if (availableSupervisors.Any()) readinessScore += 30;
        if (farmersList.Count >= 5) readinessScore += 30;

        response.Readiness = new ClusterReadinessInfo
        {
            IsReadyToFormGroups = !blockingIssues.Any(),
            AvailablePlots = plotsList.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plotsWithoutPolygon,
            AvailableSupervisors = availableSupervisors.Count,
            AvailableFarmers = farmersList.Count,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations,
            ReadinessScore = readinessScore
        };
    }

    private async Task PopulateRiceVarietySelectionAsync(
        ClusterCurrentSeasonResponse response,
        Guid seasonId,
        List<Farmer> farmersList,
        List<Plot> plotsList)
    {
        var farmerIds = farmersList.Select(f => f.Id).ToList();
        var totalFarmers = farmersList.Count;
        var plotIds = plotsList.Select(p => p.Id).ToList();
        var plotToFarmerId = plotsList.ToDictionary(p => p.Id, p => p.FarmerId);

        var cultivationsList = (await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId)).ToList();

        var farmersWithSelection = cultivationsList
            .Select(pc => plotToFarmerId[pc.PlotId])
            .Distinct()
            .ToList();

        var farmersPending = totalFarmers - farmersWithSelection.Count;
        var completionRate = totalFarmers > 0 ? (decimal)farmersWithSelection.Count / totalFarmers * 100 : 0;

        var previousSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
        var previousSeason = previousSeasons
            .OrderByDescending(s => s.CreatedAt)
            .ElementAtOrDefault(1);

        var previousCultivations = previousSeason != null
            ? (await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == previousSeason.Id)).ToList()
            : new List<PlotCultivation>();

        var varietyIds = cultivationsList.Select(pc => pc.RiceVarietyId).Distinct().ToList();
        var varieties = await _unitOfWork.Repository<RiceVariety>()
            .ListAsync(rv => varietyIds.Contains(rv.Id));
        var varietyDict = varieties.ToDictionary(rv => rv.Id);

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
                    VarietyName = varietyDict.GetValueOrDefault(varietyId)?.VarietyName ?? "Unknown",
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
                .Select(pc => varietyDict.GetValueOrDefault(pc.RiceVarietyId)?.VarietyName)
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

    private async Task<(Season? season, int year)> GetCurrentSeasonAndYear()
    {
        var today = DateTime.Now;
        var allSeasons = await _unitOfWork.Repository<Season>()
            .ListAsync(_ => true);

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

