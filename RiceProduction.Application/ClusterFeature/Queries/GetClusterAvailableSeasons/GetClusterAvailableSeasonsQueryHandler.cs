using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterAvailableSeasons;

public class GetClusterAvailableSeasonsQueryHandler
    : IRequestHandler<GetClusterAvailableSeasonsQuery, Result<ClusterSeasonsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetClusterAvailableSeasonsQueryHandler> _logger;

    public GetClusterAvailableSeasonsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetClusterAvailableSeasonsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ClusterSeasonsResponse>> Handle(
        GetClusterAvailableSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _context.Clusters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (cluster == null)
            {
                return Result<ClusterSeasonsResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            var allSeasons = await _context.Seasons
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var seasonsList = allSeasons.OrderBy(s => s.CreatedAt).ToList();

            var (currentSeason, currentYear) = ResolveCurrentSeasonAndYear(allSeasons);

            var groupsList = await _context.Groups
                .AsNoTracking()
                .Where(g => g.ClusterId == cluster.Id)
                .Select(g => new
                {
                    g.Id,
                    SeasonId = g.YearSeason != null ? (Guid?)g.YearSeason.SeasonId : null,
                    g.Year,
                    g.TotalArea
                })
                .ToListAsync(cancellationToken);

            var response = new ClusterSeasonsResponse();

            if (currentSeason != null)
            {
                var currentGroups = groupsList
                    .Where(g => g.SeasonId == currentSeason.Id && g.Year == currentYear)
                    .ToList();

                var (completionRate, pending, canFormGroups) = await CalculateSelectionProgressAsync(
                    cluster.Id, currentSeason.Id, cancellationToken);

                response.CurrentSeason = new CurrentSeasonOption
                {
                    SeasonId = currentSeason.Id,
                    SeasonName = currentSeason.SeasonName,
                    Year = currentYear,
                    DisplayName = $"{currentSeason.SeasonName} {currentYear}",
                    IsCurrent = true,
                    HasGroups = currentGroups.Any(),
                    GroupCount = currentGroups.Count,
                    TotalPlots = await GetPlotCountForGroupsAsync(
                        currentGroups.Select(g => g.Id).ToList(), cancellationToken),
                    TotalArea = currentGroups.Sum(g => g.TotalArea ?? 0),
                    SelectionProgress = completionRate,
                    SelectionsPending = pending,
                    CanFormGroups = canFormGroups
                };
            }

            var pastSeasons = new List<SeasonOption>();
            var upcomingSeasons = new List<SeasonOption>();

            foreach (var season in seasonsList)
            {
                if (season.Id == currentSeason?.Id)
                {
                    continue;
                }

                bool isPast = IsSeasonPast(season, currentSeason);
                var year = DetermineYearForSeason(season);

                var seasonGroups = groupsList
                    .Where(g => g.SeasonId == season.Id && g.Year == year)
                    .ToList();

                if (!request.IncludeEmpty && !seasonGroups.Any())
                {
                    continue;
                }

                var seasonOption = new SeasonOption
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,
                    Year = year,
                    DisplayName = $"{season.SeasonName} {year}",
                    IsCurrent = false,
                    HasGroups = seasonGroups.Any(),
                    GroupCount = seasonGroups.Count,
                    TotalPlots = await GetPlotCountForGroupsAsync(
                        seasonGroups.Select(g => g.Id).ToList(), cancellationToken),
                    TotalArea = seasonGroups.Sum(g => g.TotalArea ?? 0)
                };

                if (isPast)
                    pastSeasons.Add(seasonOption);
                else
                    upcomingSeasons.Add(seasonOption);
            }

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
        CancellationToken cancellationToken)
    {
        var farmerIds = await _context.Farmers
            .AsNoTracking()
            .Where(f => f.ClusterId == clusterId)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        var totalFarmers = farmerIds.Count;

        if (totalFarmers == 0)
        {
            return (0, 0, false);
        }

        var plotsList = await _context.Plots
            .AsNoTracking()
            .Where(p => farmerIds.Contains(p.FarmerId))
            .Select(p => new { p.Id, p.FarmerId, HasBoundary = p.Boundary != null })
            .ToListAsync(cancellationToken);
        var plotToFarmerId = plotsList.ToDictionary(p => p.Id, p => p.FarmerId);
        var plotIds = plotsList.Select(p => p.Id).ToList();

        var cultivationPlotIds = await _context.PlotCultivations
            .AsNoTracking()
            .Where(pc => plotIds.Contains(pc.PlotId) && pc.SeasonId == seasonId)
            .Select(pc => pc.PlotId)
            .ToListAsync(cancellationToken);

        var farmersWithSelection = cultivationPlotIds
            .Select(plotId => plotToFarmerId[plotId])
            .Distinct()
            .Count();

        var pending = totalFarmers - farmersWithSelection;
        var completionRate = (decimal)farmersWithSelection / totalFarmers * 100;
        var canFormGroups = pending == 0 && plotsList.Any(p => p.HasBoundary);

        return (completionRate, pending, canFormGroups);
    }

    private async Task<int> GetPlotCountForGroupsAsync(List<Guid> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return 0;
        }

        return await _context.GroupPlots
            .AsNoTracking()
            .CountAsync(gp => groupIds.Contains(gp.GroupId), cancellationToken);
    }

    private static bool IsSeasonPast(Season season, Season? currentSeason) =>
        currentSeason != null && season.CreatedAt < currentSeason.CreatedAt;

    private static int DetermineYearForSeason(Season season)
    {
        var today = DateTime.Now;
        if (!SeasonDateHelper.TryParseMonthDay(season.StartDate, out var startMonth, out _))
        {
            return today.Year;
        }

        return startMonth >= 10 && today.Month < startMonth ? today.Year - 1 : today.Year;
    }

    private static (Season? Season, int Year) ResolveCurrentSeasonAndYear(IEnumerable<Season> seasons)
    {
        var today = DateTime.Now;

        foreach (var season in seasons)
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

