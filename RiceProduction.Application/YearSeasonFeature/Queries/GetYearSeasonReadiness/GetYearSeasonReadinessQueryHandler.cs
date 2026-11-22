using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonReadiness;

public class GetYearSeasonReadinessQueryHandler 
    : IRequestHandler<GetYearSeasonReadinessQuery, Result<YearSeasonReadinessDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetYearSeasonReadinessQueryHandler> _logger;

    public GetYearSeasonReadinessQueryHandler(
        IApplicationDbContext context,
        ILogger<GetYearSeasonReadinessQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<YearSeasonReadinessDto>> Handle(
        GetYearSeasonReadinessQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonReadinessDto>.Failure(
                    $"YearSeason with ID {request.YearSeasonId} not found");
            }

            var groups = await _context.Groups
                .AsNoTracking()
                .Where(g => g.YearSeasonId == yearSeason.Id)
                .Select(g => new GroupReadinessSummary(g.Id, g.SupervisorId, g.Status))
                .ToListAsync(cancellationToken);

            var hasGroups = groups.Any();

            var readiness = hasGroups 
                ? await BuildGroupedReadinessAsync(yearSeason, groups, cancellationToken)
                : await BuildPreGroupReadinessAsync(yearSeason, cancellationToken);

            var result = new YearSeasonReadinessDto
            {
                YearSeasonId = yearSeason.Id,
                SeasonName = yearSeason.Season?.SeasonName ?? "Unknown",
                Year = yearSeason.Year,
                ClusterName = yearSeason.Cluster?.ClusterName ?? "Unknown",
                HasGroups = hasGroups,
                Readiness = readiness
            };

            return Result<YearSeasonReadinessDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error getting readiness for YearSeason {YearSeasonId}", 
                request.YearSeasonId);
            return Result<YearSeasonReadinessDto>.Failure(
                $"Error getting readiness: {ex.Message}");
        }
    }

    private async Task<ClusterReadinessInfo> BuildPreGroupReadinessAsync(
        YearSeason yearSeason,
        CancellationToken cancellationToken)
    {
        var farmerIds = await _context.Farmers
            .AsNoTracking()
            .Where(f => f.ClusterId == yearSeason.ClusterId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        var plotsList = await _context.Plots
            .AsNoTracking()
            .Where(p => farmerIds.Contains(p.FarmerId))
            .ToListAsync(cancellationToken);

        var availableSupervisors = await _context.Supervisors
            .AsNoTracking()
            .CountAsync(s => s.IsActive && s.ClusterId == yearSeason.ClusterId, cancellationToken);

        var farmersWithSelection = await CountFarmersWithSelectionAsync(
            plotsList, yearSeason.SeasonId, cancellationToken);

        var plotsWithPolygon = plotsList.Count(p =>
            p.Boundary != null && p.Status == PlotStatus.Active);
        var plotsWithoutPolygon = plotsList.Count(p =>
            p.Boundary == null || p.Status == PlotStatus.PendingPolygon);

        var blockingIssues = new List<string>();
        var recommendations = new List<string>();

        if (plotsWithoutPolygon > 0)
        {
            blockingIssues.Add($"{plotsWithoutPolygon} plots missing polygon boundaries");
            recommendations.Add("Assign polygon drawing tasks to supervisors");
        }

        if (availableSupervisors == 0)
        {
            blockingIssues.Add("No active supervisors available");
            recommendations.Add("Add supervisors to the cluster or activate existing ones");
        }

        if (farmerIds.Count < 5)
        {
            blockingIssues.Add($"Insufficient farmers (need at least 5, have {farmerIds.Count})");
            recommendations.Add("Import more farmers to the cluster");
        }

        if (plotsWithPolygon < 5)
        {
            blockingIssues.Add($"Insufficient plots with boundaries (need at least 5, have {plotsWithPolygon})");
            recommendations.Add("Complete polygon boundaries for more plots");
        }

        EvaluateVarietySelectionReadiness(
            yearSeason, farmerIds.Count, farmersWithSelection, blockingIssues, recommendations);

        if (yearSeason.Status == SeasonStatus.Draft)
        {
            blockingIssues.Add("YearSeason is in Draft status");
            recommendations.Add("Activate the YearSeason to begin group formation");
        }

        var readinessScore = CalculatePreGroupReadinessScore(
            yearSeason, plotsWithPolygon, availableSupervisors > 0, farmerIds.Count, farmersWithSelection);

        return new ClusterReadinessInfo
        {
            IsReadyToFormGroups = !blockingIssues.Any(),
            AvailablePlots = plotsList.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plotsWithoutPolygon,
            AvailableSupervisors = availableSupervisors,
            AvailableFarmers = farmerIds.Count,
            FarmersWithSelection = farmersWithSelection,
            ReadinessScore = readinessScore,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations
        };
    }

    private sealed record GroupReadinessSummary(Guid Id, Guid? SupervisorId, GroupStatus Status);

    private async Task<ClusterReadinessInfo> BuildGroupedReadinessAsync(
        YearSeason yearSeason,
        List<GroupReadinessSummary> groups,
        CancellationToken cancellationToken)
    {
        var groupIds = groups.Select(g => g.Id).ToList();
        var groupPlots = await _context.GroupPlots
            .AsNoTracking()
            .Include(gp => gp.Plot)
            .Where(gp => groupIds.Contains(gp.GroupId))
            .ToListAsync(cancellationToken);
        var plotsInGroups = groupPlots.Select(gp => gp.Plot).ToList();

        var farmerIds = plotsInGroups.Select(p => p.FarmerId).Distinct().ToList();
        var availableFarmers = await _context.Farmers
            .AsNoTracking()
            .CountAsync(f => farmerIds.Contains(f.Id), cancellationToken);

        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();
        var availableSupervisors = await _context.Supervisors
            .AsNoTracking()
            .CountAsync(s => supervisorIds.Contains(s.Id), cancellationToken);

        var farmersWithSelection = await CountFarmersWithSelectionAsync(
            plotsInGroups, yearSeason.SeasonId, cancellationToken);

        var groupsWithoutSupervisor = groups.Count(g => !g.SupervisorId.HasValue);
        var draftGroups = groups.Count(g => g.Status == GroupStatus.Draft);

        var blockingIssues = new List<string>();
        var recommendations = new List<string>();

        if (groupsWithoutSupervisor > 0)
        {
            blockingIssues.Add($"{groupsWithoutSupervisor} groups without assigned supervisors");
            recommendations.Add("Assign supervisors to all groups");
        }

        if (draftGroups == groups.Count)
        {
            blockingIssues.Add("All groups are in Draft status");
            recommendations.Add("Activate groups to begin production planning");
        }

        var readinessScore = 50; // Base score for having groups
        if (groupsWithoutSupervisor == 0) readinessScore += 25;
        if (draftGroups == 0) readinessScore += 25;

        return new ClusterReadinessInfo
        {
            IsReadyToFormGroups = true,
            AvailablePlots = plotsInGroups.Count,
            PlotsWithPolygon = plotsInGroups.Count(p => p.Boundary != null),
            PlotsWithoutPolygon = plotsInGroups.Count(p => p.Boundary == null),
            AvailableSupervisors = availableSupervisors,
            AvailableFarmers = availableFarmers,
            FarmersWithSelection = farmersWithSelection,
            ReadinessScore = readinessScore,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations
        };
    }

    private async Task<int> CountFarmersWithSelectionAsync(
        IReadOnlyCollection<Plot> plots,
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        if (plots.Count == 0)
        {
            return 0;
        }

        var plotIds = plots.Select(p => p.Id).ToList();
        var cultivationPlotIds = await _context.PlotCultivations
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId)
            .Select(pc => pc.PlotId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var plotToFarmerId = plots
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First().FarmerId);

        return cultivationPlotIds
            .Select(plotId => plotToFarmerId[plotId])
            .Distinct()
            .Count();
    }

    private static void EvaluateVarietySelectionReadiness(
        YearSeason yearSeason,
        int totalFarmers,
        int farmersWithSelection,
        List<string> blockingIssues,
        List<string> recommendations)
    {
        if (yearSeason.RiceVarietyId != null)
        {
            return;
        }

        if (!yearSeason.AllowFarmerSelection)
        {
            blockingIssues.Add("No rice variety selected and farmer selection is not enabled");
            recommendations.Add("Either enable farmer selection (AllowFarmerSelection) or have expert select a cluster-wide rice variety");
            return;
        }

        var now = DateTime.UtcNow;
        if (yearSeason.FarmerSelectionWindowStart.HasValue && now < yearSeason.FarmerSelectionWindowStart.Value)
        {
            blockingIssues.Add($"Farmer selection window not yet open (starts {yearSeason.FarmerSelectionWindowStart.Value:yyyy-MM-dd})");
            recommendations.Add("Wait for selection window to open, or adjust the selection window dates");
        }
        else if (farmersWithSelection < totalFarmers)
        {
            var pendingFarmers = totalFarmers - farmersWithSelection;
            blockingIssues.Add($"{pendingFarmers} farmers have not selected rice variety and planting date");
            recommendations.Add("Contact pending farmers to complete their selections");
        }
    }

    private static int CalculatePreGroupReadinessScore(
        YearSeason yearSeason,
        int plotsWithPolygon,
        bool hasActiveSupervisors,
        int totalFarmers,
        int farmersWithSelection)
    {
        var score = 0;
        if (plotsWithPolygon >= 5) score += 30;
        if (hasActiveSupervisors) score += 20;
        if (totalFarmers >= 5) score += 20;

        var varietySelectionReady = yearSeason.RiceVarietyId != null ||
            (yearSeason.AllowFarmerSelection && farmersWithSelection == totalFarmers);
        if (varietySelectionReady) score += 15;

        if (yearSeason.Status == SeasonStatus.Active) score += 15;

        return score;
    }
}

