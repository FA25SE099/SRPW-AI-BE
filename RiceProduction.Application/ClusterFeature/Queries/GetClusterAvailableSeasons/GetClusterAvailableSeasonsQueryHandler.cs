using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterAvailableSeasons;

public class GetClusterAvailableSeasonsQueryHandler
    : IRequestHandler<GetClusterAvailableSeasonsQuery, Result<ClusterSeasonsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterAvailableSeasonsQueryHandler> _logger;

    public GetClusterAvailableSeasonsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterAvailableSeasonsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ClusterSeasonsResponse>> Handle(
        GetClusterAvailableSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<ClusterSeasonsResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            // Get all seasons
            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);
            var seasonsList = allSeasons.OrderBy(s => s.CreatedAt).ToList();

            // Get current season
            var currentSeasonResult = await GetCurrentSeasonAndYear();
            var currentSeason = currentSeasonResult.season;
            var currentYear = currentSeasonResult.year;

            // Get all groups for this cluster
            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.ClusterId == cluster.Id);
            var groupsList = groups.ToList();

            var response = new ClusterSeasonsResponse();

            // Process current season
            if (currentSeason != null)
            {
                var currentGroups = groupsList
                    .Where(g => g.SeasonId == currentSeason.Id && g.Year == currentYear)
                    .ToList();

                // Get selection progress for current season
                var selectionProgress = await CalculateSelectionProgressAsync(
                    cluster.Id, currentSeason.Id, currentYear, cancellationToken);

                response.CurrentSeason = new CurrentSeasonOption
                {
                    SeasonId = currentSeason.Id,
                    SeasonName = currentSeason.SeasonName,
                    Year = currentYear,
                    DisplayName = $"{currentSeason.SeasonName} {currentYear}",
                    IsCurrent = true,
                    HasGroups = currentGroups.Any(),
                    GroupCount = currentGroups.Count,
                    TotalPlots = await GetPlotCountForGroupsAsync(currentGroups),
                    TotalArea = currentGroups.Sum(g => g.TotalArea ?? 0),
                    SelectionProgress = selectionProgress.completionRate,
                    SelectionsPending = selectionProgress.pending,
                    CanFormGroups = selectionProgress.canFormGroups
                };
            }

            // Process past and upcoming seasons
            var pastSeasons = new List<SeasonOption>();
            var upcomingSeasons = new List<SeasonOption>();

            foreach (var season in seasonsList)
            {
                if (season.Id == currentSeason?.Id)
                    continue;

                // Determine if this is past or upcoming
                bool isPast = IsSeasonPast(season, currentSeason);
                var year = DetermineYearForSeason(season);

                var seasonGroups = groupsList
                    .Where(g => g.SeasonId == season.Id && g.Year == year)
                    .ToList();

                if (!request.IncludeEmpty && !seasonGroups.Any())
                    continue;

                var seasonOption = new SeasonOption
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,
                    Year = year,
                    DisplayName = $"{season.SeasonName} {year}",
                    IsCurrent = false,
                    HasGroups = seasonGroups.Any(),
                    GroupCount = seasonGroups.Count,
                    TotalPlots = await GetPlotCountForGroupsAsync(seasonGroups),
                    TotalArea = seasonGroups.Sum(g => g.TotalArea ?? 0)
                };

                if (isPast)
                    pastSeasons.Add(seasonOption);
                else
                    upcomingSeasons.Add(seasonOption);
            }

            // Sort and apply limit
            response.PastSeasons = pastSeasons
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.SeasonName)
                .Take(request.Limit ?? int.MaxValue)
                .ToList();

            response.UpcomingSeasons = upcomingSeasons
                .OrderBy(s => s.Year)
                .ThenBy(s => s.SeasonName)
                .ToList();

            return Result<ClusterSeasonsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving available seasons for cluster {ClusterId}",
                request.ClusterId);
            return Result<ClusterSeasonsResponse>.Failure(
                $"Error retrieving available seasons: {ex.Message}");
        }
    }

    private async Task<(decimal completionRate, int pending, bool canFormGroups)> CalculateSelectionProgressAsync(
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

        if (totalFarmers == 0)
            return (0, 0, false);

        // Get all plots for these farmers
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => farmerIds.Contains(p.FarmerId));
        var plotsList = plots.ToList();

        // Get cultivations for current season
        var plotIds = plotsList.Select(p => p.Id).ToList();
        var cultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId);

        var farmersWithSelection = cultivations
            .Select(pc => plotsList.First(p => p.Id == pc.PlotId).FarmerId)
            .Distinct()
            .Count();

        var pending = totalFarmers - farmersWithSelection;
        var completionRate = (decimal)farmersWithSelection / totalFarmers * 100;
        var canFormGroups = pending == 0 && plotsList.Any(p => p.Boundary != null);

        return (completionRate, pending, canFormGroups);
    }

    private async Task<int> GetPlotCountForGroupsAsync(List<Group> groups)
    {
        if (!groups.Any())
            return 0;

        var groupIds = groups.Select(g => g.Id).ToList();
        var plots = await _unitOfWork.Repository<Plot>()
            .ListAsync(p => p.GroupId.HasValue && groupIds.Contains(p.GroupId.Value));

        return plots.Count();
    }

    private bool IsSeasonPast(Season season, Season? currentSeason)
    {
        if (currentSeason == null)
            return false;

        // Simple comparison - can be enhanced
        return season.CreatedAt < currentSeason.CreatedAt;
    }

    private int DetermineYearForSeason(Season season)
    {
        var today = DateTime.Now;
        var startParts = season.StartDate.Split('/');
        if (startParts.Length != 2)
            return today.Year;

        int startMonth = int.Parse(startParts[0]);

        // If season starts late in year, it might span into next year
        if (startMonth >= 10)
        {
            return today.Month < startMonth ? today.Year - 1 : today.Year;
        }

        return today.Year;
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

