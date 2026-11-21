using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorAvailableSeasons;

public class GetSupervisorAvailableSeasonsQueryHandler
    : IRequestHandler<GetSupervisorAvailableSeasonsQuery, Result<List<AvailableSeasonYearDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetSupervisorAvailableSeasonsQueryHandler> _logger;

    public GetSupervisorAvailableSeasonsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetSupervisorAvailableSeasonsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<AvailableSeasonYearDto>>> Handle(
        GetSupervisorAvailableSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var supervisor = (await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.Id == request.SupervisorId))
                .FirstOrDefault();

            if (supervisor == null)
            {
                return Result<List<AvailableSeasonYearDto>>.Failure("Supervisor not found");
            }

            var supervisorGroups = (await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId)).ToList();

            var allSeasons = (await _unitOfWork.Repository<Season>().ListAsync()).ToList();
            var seasonDict = allSeasons.ToDictionary(s => s.Id);
            var currentYear = DateTime.Now.Year;

            var currentSeasons = allSeasons
                .Where(s => IsCurrentSeason(s, currentYear) || IsCurrentSeason(s, currentYear - 1))
                .ToList();

            var result = new List<AvailableSeasonYearDto>();

            foreach (var currentSeason in currentSeasons)
            {
                int seasonYear = GetSeasonYear(currentSeason, currentYear);

                bool hasGroupForSeason = supervisorGroups.Any(g =>
                    g.YearSeason != null && g.YearSeason.SeasonId == currentSeason.Id && g.Year == seasonYear);

                if (hasGroupForSeason)
                {
                    continue;
                }

                _logger.LogInformation("Current Season for Supervisor has no group");
                result.Add(new AvailableSeasonYearDto
                {
                    SeasonId = currentSeason.Id,
                    SeasonName = currentSeason.SeasonName,
                    SeasonType = currentSeason.SeasonType ?? "",
                    Year = seasonYear,
                    DisplayName = $"{currentSeason.SeasonName} {seasonYear}",
                    StartDate = currentSeason.StartDate,
                    EndDate = currentSeason.EndDate,
                    IsCurrent = true,
                    IsPast = false,
                    HasGroup = false,
                    GroupId = null,
                    GroupStatus = null,
                    HasProductionPlan = null,
                    PlotCount = null
                });
            }

            if (supervisorGroups.Any())
            {
                result.AddRange(await BuildGroupSeasonDtosAsync(supervisorGroups, seasonDict, cancellationToken));
            }

            var sortedResult = result
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => ParseSeasonStartMonth(s.StartDate))
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} available seasons for supervisor {SupervisorId} (including current season)",
                sortedResult.Count, request.SupervisorId);

            return Result<List<AvailableSeasonYearDto>>.Success(sortedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting available seasons for supervisor {SupervisorId}",
                request.SupervisorId);
            return Result<List<AvailableSeasonYearDto>>.Failure(
                $"Error retrieving available seasons: {ex.Message}");
        }
    }

    private async Task<List<AvailableSeasonYearDto>> BuildGroupSeasonDtosAsync(
        List<Group> supervisorGroups,
        Dictionary<Guid, Season> seasonDict,
        CancellationToken cancellationToken)
    {
        var groupedBySeasonYear = supervisorGroups
            .GroupBy(g => new { SeasonId = g.YearSeason?.SeasonId, g.Year })
            .ToList();

        var groupIds = supervisorGroups.Select(g => g.Id).ToList();
        var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
            .ListAsync(pp => groupIds.Contains(pp.GroupId!.Value));
        var groupIdsWithPlans = productionPlans
            .Where(pp => pp.GroupId.HasValue)
            .Select(pp => pp.GroupId!.Value)
            .ToHashSet();

        var plotCountDict = new Dictionary<Guid, int>();
        foreach (var groupId in groupIds)
        {
            var plots = await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(groupId, cancellationToken);
            plotCountDict[groupId] = plots.Count();
        }

        var dtos = new List<AvailableSeasonYearDto>();

        foreach (var grouping in groupedBySeasonYear)
        {
            if (!grouping.Key.SeasonId.HasValue ||
                !seasonDict.TryGetValue(grouping.Key.SeasonId.Value, out var season))
            {
                continue;
            }

            var group = grouping.First();

            dtos.Add(new AvailableSeasonYearDto
            {
                SeasonId = season.Id,
                SeasonName = season.SeasonName,
                SeasonType = season.SeasonType ?? "",
                Year = grouping.Key.Year,
                DisplayName = $"{season.SeasonName} {grouping.Key.Year}",
                StartDate = season.StartDate,
                EndDate = season.EndDate,
                IsCurrent = IsCurrentSeason(season, grouping.Key.Year),
                IsPast = IsPastSeason(season, grouping.Key.Year),
                HasGroup = true,
                GroupId = group.Id,
                GroupStatus = group.Status.ToString(),
                HasProductionPlan = groupIdsWithPlans.Contains(group.Id),
                PlotCount = plotCountDict.GetValueOrDefault(group.Id, 0)
            });
        }

        return dtos;
    }

    private static int GetSeasonYear(Season season, int referenceYear)
    {
        // For cross-year seasons starting in Nov-Dec while currently in Jan-Apr, use the start year
        if (SeasonDateHelper.TryParseMonthDay(season.StartDate, out var startMonth, out _) &&
            startMonth >= 11 &&
            DateTime.Now.Month <= 4)
        {
            return referenceYear - 1;
        }

        return referenceYear;
    }

    private static bool IsCurrentSeason(Season season, int year)
    {
        if (!SeasonDateHelper.TryGetSeasonDateRange(season, year, out var seasonStart, out var seasonEnd))
        {
            return false;
        }

        var today = DateTime.Now;
        return today >= seasonStart && today <= seasonEnd;
    }

    private static bool IsPastSeason(Season season, int year) =>
        SeasonDateHelper.TryGetSeasonDateRange(season, year, out _, out var seasonEndDate) &&
        DateTime.Now > seasonEndDate;

    private static int ParseSeasonStartMonth(string startDateStr) =>
        SeasonDateHelper.TryParseMonthDay(startDateStr, out var month, out _) ? month : 0;
}