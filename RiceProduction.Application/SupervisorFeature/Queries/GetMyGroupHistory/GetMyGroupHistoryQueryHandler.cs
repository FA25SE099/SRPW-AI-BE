using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupHistory;

public class GetMyGroupHistoryQueryHandler 
    : IRequestHandler<GetMyGroupHistoryQuery, Result<List<GroupHistorySummary>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetMyGroupHistoryQueryHandler> _logger;

    public GetMyGroupHistoryQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetMyGroupHistoryQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<GroupHistorySummary>>> Handle(
        GetMyGroupHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var supervisor = (await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.Id == request.SupervisorId))
                .FirstOrDefault();

            if (supervisor == null)
            {
                return Result<List<GroupHistorySummary>>.Failure("Supervisor not found");
            }

            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);

            Season? currentSeason = null;
            if (!request.IncludeCurrentSeason)
            {
                currentSeason = await GetCurrentSeasonAsync();
            }

            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId);

            var groupsList = groups.ToList();

            if (!request.IncludeCurrentSeason && currentSeason != null)
            {
                groupsList = groupsList.Where(g => g.YearSeason?.SeasonId != currentSeason.Id).ToList();
            }

            var history = new List<GroupHistorySummary>();

            foreach (var group in groupsList)
            {
                var season = allSeasons.FirstOrDefault(s => s.Id == group.YearSeason?.SeasonId);
                if (season == null) continue;

                var plots = await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(group.Id, cancellationToken);

                var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                    .ListAsync(pp => pp.GroupId == group.Id);

                RiceVariety? riceVariety = null;
                if (group.YearSeason?.RiceVarietyId != null)
                {
                    riceVariety = await _unitOfWork.Repository<RiceVariety>()
                        .FindAsync(rv => rv.Id == group.YearSeason.RiceVarietyId);
                }

                Cluster? cluster = null;
                cluster = await _unitOfWork.Repository<Cluster>()
                    .FindAsync(c => c.Id == group.ClusterId);

                history.Add(new GroupHistorySummary
                {
                    GroupId = group.Id,
                    GroupName = $"Group {group.Id.ToString().Substring(0, 8)}",
                    Status = group.Status.ToString(),
                    Season = new HistorySeasonInfo
                    {
                        SeasonId = season.Id,
                        SeasonName = season.SeasonName,
                        SeasonType = season.SeasonType ?? "",
                        StartDate = season.StartDate,
                        EndDate = season.EndDate,
                        IsActive = season.IsActive
                    },
                    TotalArea = group.TotalArea,
                    TotalPlots = plots.Count(),
                    RiceVarietyName = riceVariety?.VarietyName,
                    PlantingDate = group.PlantingDate,
                    ProductionPlansCount = productionPlans.Count(),
                    ClusterName = cluster?.ClusterName
                });
            }

            history = history
                .OrderByDescending(h => ParseSeasonStartDate(h.Season.StartDate))
                .ToList();

            _logger.LogInformation(
                "Retrieved {HistoryCount} historical groups for supervisor {SupervisorId}",
                history.Count, request.SupervisorId);

            return Result<List<GroupHistorySummary>>.Success(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error getting group history for supervisor {SupervisorId}", 
                request.SupervisorId);
            return Result<List<GroupHistorySummary>>.Failure(
                $"Error retrieving group history: {ex.Message}");
        }
    }

    private async Task<Season?> GetCurrentSeasonAsync()
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
                return season;
            }
        }
        
        return null;
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

    private DateTime ParseSeasonStartDate(string startDateStr)
    {
        try
        {
            var parts = startDateStr.Split('/');
            int month = int.Parse(parts[0]);
            int day = int.Parse(parts[1]);
            int year = DateTime.Now.Year;
            
            if (month > 6)
            {
                year = DateTime.Now.Year - 1;
            }
            
            return new DateTime(year, month, day);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}

