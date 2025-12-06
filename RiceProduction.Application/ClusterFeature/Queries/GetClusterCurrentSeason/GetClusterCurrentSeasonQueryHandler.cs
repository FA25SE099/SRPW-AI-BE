using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterCurrentSeason;

public class GetClusterCurrentSeasonQueryHandler
    : IRequestHandler<GetClusterCurrentSeasonQuery, Result<ClusterCurrentSeasonResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterCurrentSeasonQueryHandler> _logger;
    private readonly IClusterManagerRepository _clusterManagerRepository;

    public GetClusterCurrentSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterCurrentSeasonQueryHandler> logger
        )
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _clusterManagerRepository = _unitOfWork.ClusterManagerRepository;
    }

    public async Task<Result<ClusterCurrentSeasonResponse>> Handle(
        GetClusterCurrentSeasonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get cluster
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            // Get current season
            var currentSeasonResult = await GetCurrentSeasonAndYear();
            if (currentSeasonResult.season == null)
            {
                return Result<ClusterCurrentSeasonResponse>.Failure(
                    "No current season could be determined");
            }

            var currentSeason = currentSeasonResult.season;
            var currentYear = currentSeasonResult.year;

            // Get groups for current season
            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.ClusterId == cluster.Id &&
                               g.SeasonId == currentSeason.Id &&
                               g.Year == currentYear);

            var groupsList = groups.ToList();
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

            if (hasGroups)
            {
                // Has groups - show group details
                await PopulateGroupDetailsAsync(response, groupsList, currentSeason, cancellationToken);
            }
            else
            {
                // No groups - show readiness info
                await PopulateReadinessInfoAsync(response, cluster.Id, currentSeason.Id, cancellationToken);
            }

            // Always populate rice variety selection status
            await PopulateRiceVarietySelectionAsync(response, cluster.Id, currentSeason.Id, currentYear, cancellationToken);

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

    private async Task PopulateGroupDetailsAsync(
        ClusterCurrentSeasonResponse response,
        List<Group> groups,
        Season season,
        CancellationToken cancellationToken)
    {
        var groupIds = groups.Select(g => g.Id).ToList();

        // Get plots using many-to-many relationship
        var groupPlots = await _unitOfWork.Repository<GroupPlot>()
            .ListAsync(gp => groupIds.Contains(gp.GroupId), 
                includeProperties: q => q.Include(gp => gp.Plot));
        var plotsList = groupPlots.Select(gp => gp.Plot).ToList();

        // Get farmers
        var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => farmerIds.Contains(f.Id));

        // Get rice varieties
        var varietyIds = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .Select(g => g.RiceVarietyId!.Value)
            .Distinct()
            .ToList();

        var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
            .ListAsync(rv => varietyIds.Contains(rv.Id));
        var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

        // Get supervisors
        var supervisorIds = groups
            .Where(g => g.SupervisorId.HasValue)
            .Select(g => g.SupervisorId!.Value)
            .Distinct()
            .ToList();

        var supervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => supervisorIds.Contains(s.Id));
        var supervisorDict = supervisors.ToDictionary(s => s.Id);

        // Build group summaries
        response.Groups = groups.Select(g => new GroupSeasonSummary
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

        // Rice variety breakdown
        response.RiceVarietyBreakdown = groups
            .Where(g => g.RiceVarietyId.HasValue)
            .GroupBy(g => g.RiceVarietyId!.Value)
            .Select(g => new RiceVarietyGroupSummary
            {
                RiceVarietyId = g.Key,
                RiceVarietyName = varietyDict.GetValueOrDefault(g.Key)?.VarietyName ?? "Unknown",
                GroupCount = g.Count(),
                PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => g.Select(gr => gr.Id).Contains(gp.GroupId))),
                TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
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
        Guid seasonId,
        CancellationToken cancellationToken)
    {
        // Get all farmers in the cluster directly by ClusterId
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => f.ClusterId == clusterId);
        var farmersList = farmers.ToList();
        var farmerIds = farmersList.Select(f => f.Id).ToList();

        // Get all plots for these farmers
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => farmerIds.Contains(p.FarmerId));
        var plotsList = plots.ToList();

        // Get supervisors available
        var allSupervisors = await _unitOfWork.SupervisorRepository
            .ListAsync(s => s.IsActive);
        var availableSupervisors = allSupervisors.Where(s => s.CurrentFarmerCount < s.MaxFarmerCapacity).ToList();

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
        Guid clusterId,
        Guid seasonId,
        int year,
        CancellationToken cancellationToken)
    {
        // Get all farmers in the cluster directly by ClusterId
        var farmers = await _unitOfWork.FarmerRepository
            .ListAsync(f => f.ClusterId == clusterId);
        var farmersList = farmers.ToList();
        var farmerIds = farmersList.Select(f => f.Id).ToList();
        var totalFarmers = farmersList.Count;

        // Get all plots for these farmers
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => farmerIds.Contains(p.FarmerId));
        var plotsList = plots.ToList();

        // Get plot cultivations for current season
        var plotIds = plotsList.Select(p => p.Id).ToList();
        var currentCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId);
        var cultivationsList = currentCultivations.ToList();

        // Farmers with selection
        var farmersWithSelection = cultivationsList
            .Select(pc => plotsList.First(p => p.Id == pc.PlotId).FarmerId)
            .Distinct()
            .ToList();

        var farmersPending = totalFarmers - farmersWithSelection.Count;
        var completionRate = totalFarmers > 0 ? (decimal)farmersWithSelection.Count / totalFarmers * 100 : 0;

        // Get previous season cultivations for comparison
        var previousSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
        Season? previousSeason = null;
        // Simple logic: find previous season (can be enhanced)
        var orderedSeasons = previousSeasons.OrderByDescending(s => s.CreatedAt).ToList();
        if (orderedSeasons.Count > 1)
        {
            previousSeason = orderedSeasons[1];
        }

        List<PlotCultivation> previousCultivations = new();
        if (previousSeason != null)
        {
            var prevCults = await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == previousSeason.Id);
            previousCultivations = prevCults.ToList();
        }

        // Get variety selections
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
                    IsRecommended = true, // Can be enhanced with actual recommendation logic
                    NewSelections = 0, // Can be calculated if tracking new farmers
                    SwitchedIn = Math.Max(0, currentCount - previousCount),
                    SwitchedOut = Math.Max(0, previousCount - currentCount)
                };
            })
            .OrderByDescending(v => v.SelectedBy)
            .ToList();

        // Use farmers we already fetched
        var farmerDict = farmersList.ToDictionary(f => f.Id);

        var pendingFarmerIds = farmerIds.Except(farmersWithSelection).ToList();
        var pendingFarmers = pendingFarmerIds.Select(fid => new PendingFarmerInfo
        {
            FarmerId = fid,
            FarmerName = farmerDict.GetValueOrDefault(fid)?.FullName ?? "Unknown",
            PreviousVariety = previousCultivations
                .Where(pc => plotsList.Any(p => p.Id == pc.PlotId && p.FarmerId == fid))
                .Select(pc => varietyDict.GetValueOrDefault(pc.RiceVarietyId)?.VarietyName)
                .FirstOrDefault(),
            PlotCount = plotsList.Count(p => p.FarmerId == fid)
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
        var currentMonth = today.Month;
        var currentDay = today.Day;

        var allSeasons = await _unitOfWork.Repository<Season>()
            .ListAsync(_ => true);

        foreach (var season in allSeasons)
        {
            if (IsDateInSeasonRange(currentMonth, currentDay, season.StartDate, season.EndDate))
            {
                var startParts = season.StartDate.Split('/');
                int startMonth = int.Parse(startParts[0]);

                int year = today.Year;
                if (currentMonth < startMonth && startMonth > 6)
                {
                    year--;
                }

                return (season, year);
            }
        }

        return (null, today.Year);
    }

    private bool IsDateInSeasonRange(int month, int day, string startDateStr, string endDateStr)
    {
        try
        {
            var startParts = startDateStr.Split('/');
            var endParts = endDateStr.Split('/');

            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);

            int currentDate = month * 100 + day;
            int seasonStart = startMonth * 100 + startDay;
            int seasonEnd = endMonth * 100 + endDay;

            if (seasonStart > seasonEnd)
            {
                return currentDate >= seasonStart || currentDate <= seasonEnd;
            }
            else
            {
                return currentDate >= seasonStart && currentDate <= seasonEnd;
            }
        }
        catch
        {
            return false;
        }
    }
}

