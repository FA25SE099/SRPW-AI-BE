using MediatR;
using Microsoft.Extensions.Logging;
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
            // 1. Verify supervisor exists
            var supervisor = (await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.Id == request.SupervisorId))
                .FirstOrDefault();

            if (supervisor == null)
            {
                return Result<List<AvailableSeasonYearDto>>.Failure("Supervisor not found");
            }

            // 2. Get all groups for this supervisor
            var supervisorGroups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId);

            if (!supervisorGroups.Any())
            {
                return Result<List<AvailableSeasonYearDto>>.Success(
                    new List<AvailableSeasonYearDto>(),
                    "No groups found for this supervisor");
            }

            // 3. Group by SeasonId + Year
            var groupedBySeasonYear = supervisorGroups
                .GroupBy(g => new { g.SeasonId, g.Year })
                .ToList();

            // 4. Load all seasons
            var seasonIds = groupedBySeasonYear
                .Where(g => g.Key.SeasonId.HasValue)
                .Select(g => g.Key.SeasonId!.Value)
                .Distinct()
                .ToList();

            var seasons = await _unitOfWork.Repository<Season>()
                .ListAsync(s => seasonIds.Contains(s.Id));
            var seasonDict = seasons.ToDictionary(s => s.Id);

            // 5. Load production plans for these groups
            var groupIds = supervisorGroups.Select(g => g.Id).ToList();
            var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                .ListAsync(pp => groupIds.Contains(pp.GroupId!.Value));
            var plansDict = productionPlans
                .GroupBy(pp => pp.GroupId)
                .ToDictionary(g => g.Key!.Value, g => g.ToList());

            // 6. Load plot counts for each group
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => groupIds.Contains(p.GroupId!.Value));
            var plotCountDict = plots
                .GroupBy(p => p.GroupId)
                .ToDictionary(g => g.Key!.Value, g => g.Count());

            // 7. Build result list
            var result = new List<AvailableSeasonYearDto>();

            foreach (var grouping in groupedBySeasonYear)
            {
                if (!grouping.Key.SeasonId.HasValue)
                    continue;

                var season = seasonDict.GetValueOrDefault(grouping.Key.SeasonId.Value);
                if (season == null)
                    continue;

                var group = grouping.First(); // Get first group in this season+year combo
                var hasPlan = plansDict.ContainsKey(group.Id) && plansDict[group.Id].Any();
                var plotCount = plotCountDict.GetValueOrDefault(group.Id, 0);

                var dto = new AvailableSeasonYearDto
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
                    HasProductionPlan = hasPlan,
                    PlotCount = plotCount
                };

                result.Add(dto);
            }

            // 8. Sort by year (newest first), then by season start date
            var sortedResult = result
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => ParseSeasonStartMonth(s.StartDate))
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} available seasons for supervisor {SupervisorId}",
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

    private bool IsCurrentSeason(Season season, int year)
    {
        var today = DateTime.Now;
        
        try
        {
            var startParts = season.StartDate.Split('/');
            var endParts = season.EndDate.Split('/');
            
            int startMonth = int.Parse(startParts[0]);
            int startDay = int.Parse(startParts[1]);
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);
            
            int actualEndYear = year;
            if (endMonth < startMonth) // Cross-year season
            {
                actualEndYear = year + 1;
            }
            
            var seasonStart = new DateTime(year, startMonth, startDay);
            var seasonEnd = new DateTime(actualEndYear, endMonth, endDay);
            
            return today >= seasonStart && today <= seasonEnd;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPastSeason(Season season, int year)
    {
        var today = DateTime.Now;
        
        try
        {
            var endParts = season.EndDate.Split('/');
            int endMonth = int.Parse(endParts[0]);
            int endDay = int.Parse(endParts[1]);
            
            var startParts = season.StartDate.Split('/');
            int startMonth = int.Parse(startParts[0]);
            int actualEndYear = year;
            
            if (endMonth < startMonth) // Cross-year season
            {
                actualEndYear = year + 1;
            }
            
            var seasonEndDate = new DateTime(actualEndYear, endMonth, endDay);
            return today > seasonEndDate;
        }
        catch
        {
            return false;
        }
    }

    private int ParseSeasonStartMonth(string startDateStr)
    {
        try
        {
            var parts = startDateStr.Split('/');
            return int.Parse(parts[0]);
        }
        catch
        {
            return 0;
        }
    }
}

