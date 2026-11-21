using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupThisSeason;

public class GetMyGroupThisSeasonQueryHandler 
    : IRequestHandler<GetMyGroupThisSeasonQuery, Result<MyGroupResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetMyGroupThisSeasonQueryHandler> _logger;

    public GetMyGroupThisSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetMyGroupThisSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MyGroupResponse>> Handle(
        GetMyGroupThisSeasonQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify supervisor exists using SupervisorRepository
            var supervisor = (await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.Id == request.SupervisorId))
                .FirstOrDefault();

            if (supervisor == null)
            {
                return Result<MyGroupResponse>.Failure("Supervisor not found");
            }

            var (targetSeason, seasonError) = await ResolveTargetSeasonAsync(request.SeasonId);
            if (targetSeason == null)
            {
                return Result<MyGroupResponse>.Failure(seasonError!);
            }

            var groups = await _unitOfWork.Repository<Group>()
                .GetQueryable()
                .Include(g => g.YearSeason)
                    .ThenInclude(ys => ys.RiceVariety)
                .Where(g => g.SupervisorId == request.SupervisorId &&
                            g.YearSeason != null && g.YearSeason.SeasonId == targetSeason.Id)
                .ToListAsync(cancellationToken);

            var group = groups.FirstOrDefault();

            if (group == null)
            {
                return Result<MyGroupResponse>.Failure(
                    $"No group assigned to this supervisor for season '{targetSeason.SeasonName}'");
            }

            var plotsList = (await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(group.Id, cancellationToken)).ToList();

            var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
            var farmers = await _unitOfWork.FarmerRepository.ListAsync(f => farmerIds.Contains(f.Id));
            var farmerDict = farmers.ToDictionary(f => f.Id);

            RiceVariety? riceVariety = null;
            if (group.YearSeason?.RiceVarietyId != null)
            {
                riceVariety = await _unitOfWork.Repository<RiceVariety>()
                    .FindAsync(rv => rv.Id == group.YearSeason.RiceVarietyId);
            }

            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == group.ClusterId);

            var plansList = (await _unitOfWork.Repository<ProductionPlan>()
                .ListAsync(pp => pp.GroupId == group.Id)).ToList();

            var readiness = CalculateReadiness(group, plotsList, plansList);

            var plotDetails = plotsList.Select(plot =>
            {
                var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);
                return new PlotDetail
                {
                    PlotId = plot.Id,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    SoilType = plot.SoilType,
                    HasPolygon = plot.Boundary != null,
                    PolygonGeoJson = PlotGeometryHelper.SerializeGeometry(plot.Boundary),
                    CentroidGeoJson = PlotGeometryHelper.SerializeGeometry(plot.Coordinate),
                    Status = plot.Status.ToString(),
                    FarmerId = plot.FarmerId,
                    FarmerName = farmer?.FullName,
                    FarmerPhone = farmer?.PhoneNumber,
                    FarmerAddress = farmer?.Address,
                    FarmCode = farmer?.FarmCode
                };
            }).OrderBy(p => p.SoThua).ThenBy(p => p.SoTo).ToList();

            var plansSummary = new ProductionPlansSummary
            {
                TotalPlans = plansList.Count,
                DraftPlans = plansList.Count(p => p.Status == TaskStatus.Draft),
                ActivePlans = plansList.Count(p => p.Status == TaskStatus.InProgress),
                ApprovedPlans = plansList.Count(p => p.Status == TaskStatus.Approved),
                HasActiveProductionPlan = plansList.Any(p =>
                    p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress)
            };

            var response = new MyGroupResponse
            {
                GroupId = group.Id,
                GroupName = $"Group {group.Id.ToString().Substring(0, 8)}",
                Status = group.Status.ToString(),
                PlantingDate = group.PlantingDate,
                TotalArea = group.TotalArea,
                AreaGeoJson = PlotGeometryHelper.SerializeGeometry(group.Area),
                RiceVarietyId = group.YearSeason?.RiceVarietyId,
                RiceVarietyName = riceVariety?.VarietyName,
                Season = new SeasonInfo
                {
                    SeasonId = targetSeason.Id,
                    SeasonName = targetSeason.SeasonName,
                    SeasonType = targetSeason.SeasonType ?? "",
                    StartDate = targetSeason.StartDate,
                    EndDate = targetSeason.EndDate,
                    IsActive = targetSeason.IsActive
                },
                ClusterId = group.ClusterId,
                ClusterName = cluster?.ClusterName,
                Plots = plotDetails,
                Readiness = readiness,
                ProductionPlans = plansSummary
            };

            _logger.LogInformation(
                "Retrieved group {GroupId} for supervisor {SupervisorId} in season {SeasonName}",
                group.Id, request.SupervisorId, targetSeason.SeasonName);

            return Result<MyGroupResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting group for supervisor {SupervisorId}",
                request.SupervisorId);
            return Result<MyGroupResponse>.Failure(
                $"Error retrieving group: {ex.Message}");
        }
    }

    private async Task<(Season? Season, string? Error)> ResolveTargetSeasonAsync(Guid? seasonId)
    {
        if (seasonId.HasValue)
        {
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == seasonId.Value);

            return season != null ? (season, null) : (null, "Season not found");
        }

        var currentSeason = await GetCurrentSeasonAsync();
        return currentSeason != null ? (currentSeason, null) : (null, "No current season could be determined");
    }

    private static GroupReadinessInfo CalculateReadiness(
        Group group,
        List<Plot> plots,
        List<ProductionPlan> productionPlans)
    {
        int plotsWithPolygon = plots.Count(p => p.Boundary != null);
        var readiness = new GroupReadinessInfo
        {
            TotalPlots = plots.Count,
            PlotsWithPolygon = plotsWithPolygon,
            PlotsWithoutPolygon = plots.Count - plotsWithPolygon
        };

        var blocking = new List<string>();
        var warnings = new List<string>();

        readiness.HasRiceVariety = group.YearSeason?.RiceVarietyId != null;
        if (!readiness.HasRiceVariety)
            blocking.Add("Rice variety not selected");

        readiness.HasTotalArea = group.TotalArea.HasValue && group.TotalArea.Value > 0;
        if (!readiness.HasTotalArea)
            blocking.Add("Total area not defined");

        readiness.HasPlots = plots.Any();
        if (!readiness.HasPlots)
            blocking.Add("No plots assigned to this group");

        readiness.AllPlotsHavePolygons = plots.Any() && readiness.PlotsWithoutPolygon == 0;
        if (!readiness.AllPlotsHavePolygons && plots.Any())
            blocking.Add($"{readiness.PlotsWithoutPolygon} plot(s) missing polygon boundaries");

        if (group.Status != GroupStatus.Active && group.Status != GroupStatus.ReadyForOptimization)
            warnings.Add($"Group status is {group.Status}");

        if (!group.PlantingDate.HasValue)
            warnings.Add("No planting date set (can be specified when creating production plan)");

        if (productionPlans.Any(p => p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress))
            warnings.Add("Active production plan already exists");

        readiness.BlockingIssues = blocking;
        readiness.Warnings = warnings;

        const int totalRequired = 4;
        int passed = (readiness.HasRiceVariety ? 1 : 0)
                   + (readiness.HasTotalArea ? 1 : 0)
                   + (readiness.HasPlots ? 1 : 0)
                   + (readiness.AllPlotsHavePolygons ? 1 : 0);

        readiness.ReadinessScore = (int)((passed / (double)totalRequired) * 100);
        readiness.IsReady = blocking.Count == 0;
        readiness.ReadinessLevel = readiness.IsReady
            ? "Ready"
            : readiness.ReadinessScore switch
            {
                >= 75 => "Almost Ready",
                >= 50 => "In Progress",
                _ => "Not Ready"
            };

        return readiness;
    }

    private async Task<Season?> GetCurrentSeasonAsync()
    {
        var today = DateTime.Now;
        var allSeasons = await _unitOfWork.Repository<Season>().ListAsync(_ => true);
        return allSeasons.FirstOrDefault(s =>
            SeasonDateHelper.IsDateInSeasonRange(today.Month, today.Day, s.StartDate, s.EndDate));
    }
}

