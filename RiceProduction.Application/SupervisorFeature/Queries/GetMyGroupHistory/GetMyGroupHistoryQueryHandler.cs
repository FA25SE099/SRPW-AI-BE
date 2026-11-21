using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
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

            var allSeasons = (await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true)).ToList();
            var seasonDict = allSeasons.ToDictionary(s => s.Id);

            Season? currentSeason = null;
            if (!request.IncludeCurrentSeason)
            {
                var today = DateTime.Now;
                currentSeason = allSeasons.FirstOrDefault(s =>
                    SeasonDateHelper.IsDateInSeasonRange(today.Month, today.Day, s.StartDate, s.EndDate));
            }

            var groupsList = (await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId)).ToList();

            if (!request.IncludeCurrentSeason && currentSeason != null)
            {
                groupsList = groupsList
                    .Where(g => g.YearSeason?.SeasonId != currentSeason.Id)
                    .ToList();
            }

            var history = new List<GroupHistorySummary>();

            foreach (var group in groupsList)
            {
                if (group.YearSeason?.SeasonId is not { } seasonId ||
                    !seasonDict.TryGetValue(seasonId, out var season))
                {
                    continue;
                }

                var plots = await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(group.Id, cancellationToken);
                var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                    .ListAsync(pp => pp.GroupId == group.Id);

                RiceVariety? riceVariety = null;
                if (group.YearSeason.RiceVarietyId != null)
                {
                    riceVariety = await _unitOfWork.Repository<RiceVariety>()
                        .FindAsync(rv => rv.Id == group.YearSeason.RiceVarietyId);
                }

                var cluster = await _unitOfWork.Repository<Cluster>()
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

    private static DateTime ParseSeasonStartDate(string startDateStr)
    {
        if (!SeasonDateHelper.TryParseMonthDay(startDateStr, out var month, out var day))
        {
            return DateTime.MinValue;
        }

        try
        {
            var currentYear = DateTime.Now.Year;
            var year = month > 6 ? currentYear - 1 : currentYear;
            return new DateTime(year, month, day);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}

