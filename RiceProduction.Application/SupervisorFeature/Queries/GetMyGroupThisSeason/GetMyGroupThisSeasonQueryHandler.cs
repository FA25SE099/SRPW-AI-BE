using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
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

            // Determine which season to use
            Season? targetSeason = null;
            if (request.SeasonId.HasValue)
            {
                targetSeason = await _unitOfWork.Repository<Season>()
                    .FindAsync(s => s.Id == request.SeasonId.Value);
                
                if (targetSeason == null)
                {
                    return Result<MyGroupResponse>.Failure("Season not found");
                }
            }
            else
            {
                targetSeason = await GetCurrentSeasonAsync();
                
                if (targetSeason == null)
                {
                    return Result<MyGroupResponse>.Failure("No current season could be determined");
                }
            }

            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => g.SupervisorId == request.SupervisorId && g.SeasonId == targetSeason.Id);

            var group = groups.FirstOrDefault();

            if (group == null)
            {
                return Result<MyGroupResponse>.Failure(
                    $"No group assigned to this supervisor for season '{targetSeason.SeasonName}'");
            }

            var plots = await _unitOfWork.PlotRepository.GetPlotsForGroupAsync(group.Id, cancellationToken);

            var plotsList = plots.ToList();

            var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => farmerIds.Contains(f.Id));
            var farmerDict = farmers.ToDictionary(f => f.Id);

            RiceVariety? riceVariety = null;
            if (group.RiceVarietyId.HasValue)
            {
                riceVariety = await _unitOfWork.Repository<RiceVariety>()
                    .FindAsync(rv => rv.Id == group.RiceVarietyId.Value);
            }

            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == group.ClusterId);

            var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                .ListAsync(pp => pp.GroupId == group.Id);

            // Calculate readiness
            var readiness = CalculateReadiness(group, plotsList, productionPlans.ToList());

            var plotDetails = plotsList.Select(plot => new PlotDetail
            {
                PlotId = plot.Id,
                SoThua = plot.SoThua,
                SoTo = plot.SoTo,
                Area = plot.Area,
                SoilType = plot.SoilType,
                HasPolygon = plot.Boundary != null,
                PolygonGeoJson = plot.Boundary != null ? SerializeGeometry(plot.Boundary) : null,
                CentroidGeoJson = plot.Coordinate != null ? SerializeGeometry(plot.Coordinate) : null,
                Status = plot.Status.ToString(),
                FarmerId = plot.FarmerId,
                FarmerName = farmerDict.GetValueOrDefault(plot.FarmerId)?.FullName,
                FarmerPhone = farmerDict.GetValueOrDefault(plot.FarmerId)?.PhoneNumber,
                FarmerAddress = farmerDict.GetValueOrDefault(plot.FarmerId)?.Address,
                FarmCode = farmerDict.GetValueOrDefault(plot.FarmerId)?.FarmCode
            }).OrderBy(p => p.SoThua).ThenBy(p => p.SoTo).ToList();

            // Build production plans summary
            var plansList = productionPlans.ToList();
            var plansSummary = new ProductionPlansSummary
            {
                TotalPlans = plansList.Count,
                DraftPlans = plansList.Count(p => p.Status == TaskStatus.Draft),
                ActivePlans = plansList.Count(p => p.Status == TaskStatus.InProgress),
                ApprovedPlans = plansList.Count(p => p.Status == TaskStatus.Approved),
                HasActiveProductionPlan = plansList.Any(p => 
                    p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress)
            };

            // Build response
            var response = new MyGroupResponse
            {
                GroupId = group.Id,
                GroupName = $"Group {group.Id.ToString().Substring(0, 8)}",
                Status = group.Status.ToString(),
                PlantingDate = group.PlantingDate,
                TotalArea = group.TotalArea,
                AreaGeoJson = group.Area != null ? SerializeGeometry(group.Area) : null,
                RiceVarietyId = group.RiceVarietyId,
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

    private GroupReadinessInfo CalculateReadiness(
        Group group, 
        List<Plot> plots, 
        List<ProductionPlan> productionPlans)
    {
        var readiness = new GroupReadinessInfo
        {
            TotalPlots = plots.Count,
            PlotsWithPolygon = plots.Count(p => p.Boundary != null),
            PlotsWithoutPolygon = plots.Count(p => p.Boundary == null)
        };

        var blocking = new List<string>();
        var warnings = new List<string>();

        // Check 1: Rice Variety
        readiness.HasRiceVariety = group.RiceVarietyId.HasValue;
        if (!readiness.HasRiceVariety)
        {
            blocking.Add("Rice variety not selected");
        }

        // Check 2: Total Area
        readiness.HasTotalArea = group.TotalArea.HasValue && group.TotalArea.Value > 0;
        if (!readiness.HasTotalArea)
        {
            blocking.Add("Total area not defined");
        }

        // Check 3: Has Plots
        readiness.HasPlots = plots.Any();
        if (!readiness.HasPlots)
        {
            blocking.Add("No plots assigned to this group");
        }

        // Check 4: All Plots Have Polygons
        readiness.AllPlotsHavePolygons = plots.Any() && plots.All(p => p.Boundary != null);
        if (!readiness.AllPlotsHavePolygons && plots.Any())
        {
            blocking.Add($"{readiness.PlotsWithoutPolygon} plot(s) missing polygon boundaries");
        }

        // Warnings
        if (group.Status != GroupStatus.Active && group.Status != GroupStatus.ReadyForOptimization)
        {
            warnings.Add($"Group status is {group.Status}");
        }

        if (!group.PlantingDate.HasValue)
        {
            warnings.Add("No planting date set (can be specified when creating production plan)");
        }

        if (productionPlans.Any(p => p.Status == TaskStatus.Approved || p.Status == TaskStatus.InProgress))
        {
            warnings.Add($"Active production plan already exists");
        }

        readiness.BlockingIssues = blocking;
        readiness.Warnings = warnings;

        // Calculate score
        int totalRequired = 4; // variety, area, plots, polygons
        int passed = 0;
        if (readiness.HasRiceVariety) passed++;
        if (readiness.HasTotalArea) passed++;
        if (readiness.HasPlots) passed++;
        if (readiness.AllPlotsHavePolygons) passed++;

        readiness.ReadinessScore = (int)((passed / (double)totalRequired) * 100);
        readiness.IsReady = blocking.Count == 0;

        // Determine level
        if (readiness.IsReady)
            readiness.ReadinessLevel = "Ready";
        else if (readiness.ReadinessScore >= 75)
            readiness.ReadinessLevel = "Almost Ready";
        else if (readiness.ReadinessScore >= 50)
            readiness.ReadinessLevel = "In Progress";
        else
            readiness.ReadinessLevel = "Not Ready";

        return readiness;
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

    private string? SerializeGeometry(NetTopologySuite.Geometries.Geometry? geometry)
    {
        if (geometry == null) return null;
        try
        {
            var writer = new GeoJsonWriter();
            return writer.Write(geometry);
        }
        catch
        {
            return null;
        }
    }
}

