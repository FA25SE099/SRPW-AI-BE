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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetYearSeasonReadinessQueryHandler> _logger;

    public GetYearSeasonReadinessQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetYearSeasonReadinessQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<YearSeasonReadinessDto>> Handle(
        GetYearSeasonReadinessQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load YearSeason
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .FirstOrDefaultAsync(ys => ys.Id == request.YearSeasonId, cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonReadinessDto>.Failure(
                    $"YearSeason with ID {request.YearSeasonId} not found");
            }

            // Check if groups already exist
            var groups = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .Where(g => g.YearSeasonId == yearSeason.Id)
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
        // Get all farmers in the cluster
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => f.ClusterId == yearSeason.ClusterId);
        var farmersList = farmers.ToList();
        var farmerIds = farmersList.Select(f => f.Id).ToList();

        // Get all plots for these farmers
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => farmerIds.Contains(p.FarmerId));
        var plotsList = plots.ToList();
        var plotIds = plotsList.Select(p => p.Id).ToList();

        // Get active supervisors
        var supervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => s.IsActive && s.ClusterId == yearSeason.ClusterId);
        var supervisorsList = supervisors.ToList();

        // Check farmer selections (PlotCultivations for this season)
        var cultivations = await _unitOfWork.Repository<PlotCultivation>()
            .GetQueryable()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
            .ToListAsync(cancellationToken);

        var farmersWithSelection = cultivations
            .Select(pc => plotsList.First(p => p.Id == pc.PlotId).FarmerId)
            .Distinct()
            .Count();

        // Analyze readiness
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

        if (!supervisorsList.Any())
        {
            blockingIssues.Add("No active supervisors available");
            recommendations.Add("Add supervisors to the cluster or activate existing ones");
        }

        if (farmersList.Count < 5)
        {
            blockingIssues.Add($"Insufficient farmers (need at least 5, have {farmersList.Count})");
            recommendations.Add("Import more farmers to the cluster");
        }

        if (plotsWithPolygon < 5)
        {
            blockingIssues.Add($"Insufficient plots with boundaries (need at least 5, have {plotsWithPolygon})");
            recommendations.Add("Complete polygon boundaries for more plots");
        }

        // Check if using expert-selected variety or farmer selection model
        if (yearSeason.RiceVarietyId == null)
        {
            // Farmer selection model - check if farmers have made their selections
            if (yearSeason.AllowFarmerSelection)
            {
                var now = DateTime.UtcNow;
                var isSelectionWindowOpen = 
                    (!yearSeason.FarmerSelectionWindowStart.HasValue || now >= yearSeason.FarmerSelectionWindowStart.Value) &&
                    (!yearSeason.FarmerSelectionWindowEnd.HasValue || now <= yearSeason.FarmerSelectionWindowEnd.Value);

                if (!isSelectionWindowOpen && yearSeason.FarmerSelectionWindowStart.HasValue && now < yearSeason.FarmerSelectionWindowStart.Value)
                {
                    blockingIssues.Add($"Farmer selection window not yet open (starts {yearSeason.FarmerSelectionWindowStart.Value:yyyy-MM-dd})");
                    recommendations.Add("Wait for selection window to open, or adjust the selection window dates");
                }
                else if (farmersWithSelection < farmersList.Count)
                {
                    var pendingFarmers = farmersList.Count - farmersWithSelection;
                    blockingIssues.Add($"{pendingFarmers} farmers have not selected rice variety and planting date");
                    recommendations.Add("Contact pending farmers to complete their selections");
                }
            }
            else
            {
                blockingIssues.Add("No rice variety selected and farmer selection is not enabled");
                recommendations.Add("Either enable farmer selection (AllowFarmerSelection) or have expert select a cluster-wide rice variety");
            }
        }

        if (yearSeason.Status == SeasonStatus.Draft)
        {
            blockingIssues.Add("YearSeason is in Draft status");
            recommendations.Add("Activate the YearSeason to begin group formation");
        }

        // Calculate readiness score
        var readinessScore = 0;
        if (plotsWithPolygon >= 5) readinessScore += 30;
        if (supervisorsList.Any()) readinessScore += 20;
        if (farmersList.Count >= 5) readinessScore += 20;
        
        // Variety/selection score
        if (yearSeason.RiceVarietyId != null)
        {
            // Expert selected variety for cluster
            readinessScore += 15;
        }
        else if (yearSeason.AllowFarmerSelection && farmersWithSelection == farmersList.Count)
        {
            // All farmers have made selections
            readinessScore += 15;
        }
        
        if (yearSeason.Status == SeasonStatus.Active) readinessScore += 15;

        return new ClusterReadinessInfo
        {
            IsReadyToFormGroups = !blockingIssues.Any(),
            AvailablePlots = plotsList.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plotsWithoutPolygon,
            AvailableSupervisors = supervisorsList.Count,
            AvailableFarmers = farmersList.Count,
            FarmersWithSelection = farmersWithSelection,
            ReadinessScore = readinessScore,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations
        };
    }

    private async Task<ClusterReadinessInfo> BuildGroupedReadinessAsync(
        YearSeason yearSeason,
        List<Group> groups,
        CancellationToken cancellationToken)
    {
        // Get all plot IDs in groups
        var groupIds = groups.Select(g => g.Id).ToList();
        var groupPlots = await _unitOfWork.Repository<GroupPlot>()
            .ListAsync(gp => groupIds.Contains(gp.GroupId),
                includeProperties: q => q.Include(gp => gp.Plot));
        var plotsInGroups = groupPlots.Select(gp => gp.Plot).ToList();
        var plotIds = plotsInGroups.Select(p => p.Id).ToList();

        // Get farmers in groups
        var farmerIds = plotsInGroups.Select(p => p.FarmerId).Distinct().ToList();
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => farmerIds.Contains(f.Id));

        // Get supervisors
        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();
        var supervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => supervisorIds.Contains(s.Id));

        // Get farmer selections
        var cultivations = await _unitOfWork.Repository<PlotCultivation>()
            .GetQueryable()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == yearSeason.SeasonId)
            .ToListAsync(cancellationToken);

        var farmersWithSelection = cultivations
            .Select(pc => plotsInGroups.First(p => p.Id == pc.PlotId).FarmerId)
            .Distinct()
            .Count();

        // Analyze grouped readiness
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

        // Calculate readiness score (groups already formed)
        var readinessScore = 50; // Base score for having groups
        if (groupsWithoutSupervisor == 0) readinessScore += 25;
        if (draftGroups == 0) readinessScore += 25;

        return new ClusterReadinessInfo
        {
            IsReadyToFormGroups = true, // Already have groups
            AvailablePlots = plotsInGroups.Count,
            PlotsWithPolygon = plotsInGroups.Count(p => p.Boundary != null),
            PlotsWithoutPolygon = plotsInGroups.Count(p => p.Boundary == null),
            AvailableSupervisors = supervisors.Count(),
            AvailableFarmers = farmers.Count(),
            FarmersWithSelection = farmersWithSelection,
            ReadinessScore = readinessScore,
            BlockingIssues = blockingIssues,
            Recommendations = recommendations
        };
    }
}

