using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Application.Common.Services;
using RiceProduction.Domain.Entities;
using static RiceProduction.Application.Common.Services.GroupFormationService;

namespace RiceProduction.Application.GroupFeature.Queries.PreviewGroups;

public class PreviewGroupsQueryHandler : IRequestHandler<PreviewGroupsQuery, Result<PreviewGroupsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PreviewGroupsQueryHandler> _logger;
    private readonly GroupFormationService _groupFormationService;
    private readonly GroupNameGenerationService _groupNameService;
    private readonly GeoJsonWriter _geoJsonWriter;

    public PreviewGroupsQueryHandler(
        IApplicationDbContext context,
        ILogger<PreviewGroupsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
        _groupFormationService = new GroupFormationService();
        _groupNameService = new GroupNameGenerationService();
        _geoJsonWriter = new GeoJsonWriter();
    }

    public async Task<Result<PreviewGroupsResponse>> Handle(
        PreviewGroupsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cluster = await _context.Clusters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClusterId, cancellationToken);

            if (cluster == null)
            {
                return Result<PreviewGroupsResponse>.Failure($"Không tìm thấy cụm {request.ClusterId}");
            }

            var season = await _context.Seasons
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SeasonId, cancellationToken);

            if (season == null)
            {
                return Result<PreviewGroupsResponse>.Failure($"Không tìm thấy vụ mùa {request.SeasonId}");
            }

            var parameters = new GroupingParameters
            {
                ProximityThreshold = request.ProximityThreshold ?? 100,
                PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
                MinGroupArea = request.MinGroupArea ?? 5.0m,
                MaxGroupArea = request.MaxGroupArea ?? 15,
                MinPlotsPerGroup = request.MinPlotsPerGroup ?? 3,
                MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 10
            };

            var farmersList = await _context.Farmers
                .AsNoTracking()
                .Where(f => f.ClusterId == request.ClusterId)
                .ToListAsync(cancellationToken);
            var farmerIds = farmersList.Select(f => f.Id).ToList();

            var plotsList = await _context.Plots
                .AsNoTracking()
                .Where(p => farmerIds.Contains(p.FarmerId))
                .ToListAsync(cancellationToken);
            var plotIds = plotsList.Select(p => p.Id).ToList();

            var cultivationsList = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => plotIds.Contains(pc.PlotId) &&
                             pc.SeasonId == request.SeasonId)
                .ToListAsync(cancellationToken);

            if (!cultivationsList.Any())
            {
                return Result<PreviewGroupsResponse>.Failure(
                    "Không tìm thấy canh tác thửa đất nào cho vụ mùa này. " +
                    "Nông dân phải chọn giống lúa trước khi thành lập nhóm.");
            }

            // Business rule: A plot can belong to multiple groups, but only one group per season
            var alreadyGroupedPlots = (await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => plotIds.Contains(gp.PlotId) &&
                             gp.Group.YearSeason != null &&
                             gp.Group.YearSeason.SeasonId == request.SeasonId)
                .Select(gp => gp.PlotId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var plotDict = plotsList.ToDictionary(p => p.Id);
            var plotClusterInfos = new List<PlotClusterInfo>();

            foreach (var cultivation in cultivationsList)
            {
                if (!plotDict.TryGetValue(cultivation.PlotId, out var plot))
                    continue;

                if (alreadyGroupedPlots.Contains(plot.Id))
                    continue;

                var coordinate = plot.Coordinate ?? plot.Boundary?.Centroid;

                plotClusterInfos.Add(new PlotClusterInfo
                {
                    Plot = plot,
                    PlotCultivation = cultivation,
                    Coordinate = coordinate!,
                    PlantingDate = cultivation.PlantingDate.Date != DateTime.MinValue.Date ? cultivation.PlantingDate.Date : DateTime.UtcNow.Date,
                    RiceVarietyId = cultivation.RiceVarietyId,
                    IsGrouped = false
                });
            }

            if (!plotClusterInfos.Any())
            {
                return Result<PreviewGroupsResponse>.Failure(
                    "Không tìm thấy thửa đất đủ điều kiện để nhóm. " +
                    "Đảm bảo các thửa đất có tọa độ/ranh giới và chưa được nhóm.");
            }

            var (proposedGroups, ungroupedPlots) = _groupFormationService.FormGroups(
                plotClusterInfos,
                parameters);

            var varietyIds = plotClusterInfos.Select(p => p.RiceVarietyId).Distinct().ToList();
            var riceVarieties = await _context.RiceVarieties
                .AsNoTracking()
                .Where(rv => varietyIds.Contains(rv.Id))
                .ToListAsync(cancellationToken);
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);
            var farmerDict = farmersList.ToDictionary(f => f.Id);

            var (availableSupervisors, supervisorsForAssignment) =
                await GetAvailableSupervisorsAsync(request.ClusterId, request.SeasonId, request.Year, cancellationToken);

            var response = new PreviewGroupsResponse
            {
                ClusterId = request.ClusterId,
                SeasonId = request.SeasonId,
                Year = request.Year,
                Parameters = new GroupingParametersDto
                {
                    ProximityThreshold = parameters.ProximityThreshold,
                    PlantingDateTolerance = parameters.PlantingDateTolerance,
                    MinGroupArea = parameters.MinGroupArea,
                    MaxGroupArea = parameters.MaxGroupArea,
                    MinPlotsPerGroup = parameters.MinPlotsPerGroup,
                    MaxPlotsPerGroup = parameters.MaxPlotsPerGroup
                },
                AvailableSupervisors = availableSupervisors
                    .OrderByDescending(s => s.IsAvailable)
                    .ThenBy(s => s.CurrentTotalArea)
                    .ToList(),
                Summary = new PreviewSummary
                {
                    TotalEligiblePlots = plotClusterInfos.Count,
                    PlotsGrouped = proposedGroups.Sum(g => g.Plots.Count),
                    UngroupedPlots = ungroupedPlots.Count,
                    GroupsToBeFormed = proposedGroups.Count,
                    EstimatedTotalArea = proposedGroups.Sum(g => g.TotalArea),
                    SupervisorsNeeded = proposedGroups.Count,
                    SupervisorsAvailable = supervisorsForAssignment.Count,
                    GroupsWithoutSupervisor = Math.Max(0, proposedGroups.Count - supervisorsForAssignment.Count),
                    HasSufficientSupervisors = supervisorsForAssignment.Count >= proposedGroups.Count
                },
                PreviewGroups = proposedGroups
                    .Select((g, index) => MapToPreviewGroupDto(
                        g,
                        index,
                        cluster.ClusterName,
                        season.SeasonName,
                        request.Year,
                        supervisorsForAssignment,
                        varietyDict,
                        farmerDict))
                    .ToList(),
                UngroupedPlots = ungroupedPlots
                    .Select(u => MapToUngroupedPlotDto(u, varietyDict, farmerDict))
                    .ToList()
            };

            _logger.LogInformation(
                "Preview groups for cluster {ClusterId}, season {SeasonId}: {GroupCount} groups, {PlotCount} plots grouped, {UngroupedCount} ungrouped, {SupervisorCount} supervisors available",
                request.ClusterId, request.SeasonId, response.PreviewGroups.Count,
                response.Summary.PlotsGrouped, response.Summary.UngroupedPlots, supervisorsForAssignment.Count);

            return Result<PreviewGroupsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing groups for cluster {ClusterId}", request.ClusterId);
            return Result<PreviewGroupsResponse>.Failure($"Lỗi xem trước các nhóm: {ex.Message}");
        }
    }

    private async Task<(List<SupervisorForAssignmentDto> AvailableSupervisors, List<Supervisor> SupervisorsForAssignment)>
        GetAvailableSupervisorsAsync(Guid clusterId, Guid seasonId, int year, CancellationToken cancellationToken)
    {
        var supervisorAreaCapacitySetting = await _context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == "SupervisorMaxAreaCapacity", cancellationToken);

        decimal? supervisorMaxArea = supervisorAreaCapacitySetting != null &&
            decimal.TryParse(supervisorAreaCapacitySetting.SettingValue, out var areaCapacity)
                ? areaCapacity
                : null;

        var supervisorsList = await _context.Supervisors
            .AsNoTracking()
            .Include(s => s.ManagedCluster)
            .Where(s => s.IsActive && s.ClusterId == clusterId)
            .ToListAsync(cancellationToken);

        var supervisorIds = supervisorsList.Select(s => s.Id).ToList();

        var existingGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.SupervisorId.HasValue &&
                        supervisorIds.Contains(g.SupervisorId.Value) &&
                        g.YearSeason != null &&
                        g.YearSeason.Year == year &&
                        g.YearSeason.SeasonId == seasonId)
            .ToListAsync(cancellationToken);

        var supervisorWorkload = existingGroups
            .GroupBy(g => g.SupervisorId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    GroupCount = g.Count(),
                    TotalArea = g.Sum(x => x.TotalArea ?? 0)
                });

        var availableSupervisors = new List<SupervisorForAssignmentDto>();
        var supervisorsForAssignment = new List<Supervisor>();

        foreach (var supervisor in supervisorsList)
        {
            var workload = supervisorWorkload.GetValueOrDefault(supervisor.Id);
            var currentArea = workload?.TotalArea ?? 0;
            var currentGroups = workload?.GroupCount ?? 0;

            bool isAvailable = true;
            string? unavailableReason = null;
            decimal? remainingArea = null;

            if (supervisorMaxArea.HasValue)
            {
                remainingArea = supervisorMaxArea.Value - currentArea;
                if (remainingArea <= 0)
                {
                    isAvailable = false;
                    unavailableReason = "Đã đạt giới hạn diện tích";
                }
            }

            availableSupervisors.Add(new SupervisorForAssignmentDto
            {
                SupervisorId = supervisor.Id,
                FullName = supervisor.FullName ?? "Unknown",
                PhoneNumber = supervisor.PhoneNumber,
                ClusterId = supervisor.ClusterId,
                ClusterName = supervisor.ManagedCluster?.ClusterName,
                CurrentFarmerCount = supervisor.CurrentFarmerCount,
                MaxFarmerCapacity = supervisor.MaxFarmerCapacity,
                CurrentGroupCount = currentGroups,
                CurrentTotalArea = currentArea,
                MaxAreaCapacity = supervisorMaxArea,
                RemainingAreaCapacity = remainingArea,
                IsAvailable = isAvailable,
                UnavailableReason = unavailableReason
            });

            if (isAvailable)
            {
                supervisorsForAssignment.Add(supervisor);
            }
        }

        supervisorsForAssignment = supervisorsForAssignment
            .OrderBy(s => supervisorWorkload.GetValueOrDefault(s.Id)?.TotalArea ?? 0)
            .ToList();

        return (availableSupervisors, supervisorsForAssignment);
    }

    private PreviewGroupDto MapToPreviewGroupDto(
        ProposedGroup group,
        int index,
        string clusterName,
        string seasonName,
        int year,
        List<Supervisor> supervisorsForAssignment,
        Dictionary<Guid, RiceVariety> varietyDict,
        Dictionary<Guid, Farmer> farmerDict)
    {
        Supervisor? assignedSupervisor = supervisorsForAssignment.Count > 0
            ? supervisorsForAssignment[index % supervisorsForAssignment.Count]
            : null;

        var varietyName = varietyDict.GetValueOrDefault(group.RiceVarietyId)?.VarietyName ?? "Unknown";
        var groupName = _groupNameService.GenerateGroupName(
            clusterName,
            seasonName,
            year,
            varietyName,
            group.GroupNumber);

        return new PreviewGroupDto
        {
            GroupNumber = group.GroupNumber,
            GroupName = groupName,
            RiceVarietyId = group.RiceVarietyId,
            RiceVarietyName = varietyName,
            SupervisorId = assignedSupervisor?.Id,
            SupervisorName = assignedSupervisor?.FullName,
            PlantingWindowStart = group.PlantingWindowStart,
            PlantingWindowEnd = group.PlantingWindowEnd,
            MedianPlantingDate = group.MedianPlantingDate,
            PlotCount = group.Plots.Count,
            TotalArea = group.TotalArea,
            CentroidLat = group.Centroid.Y,
            CentroidLng = group.Centroid.X,
            GroupBoundaryGeoJson = null,
            PlotIds = group.Plots.Select(p => p.Plot.Id).ToList(),
            Plots = group.Plots.Select(p =>
            {
                var farmer = farmerDict.GetValueOrDefault(p.Plot.FarmerId);
                return new PlotInGroupDto
                {
                    PlotId = p.Plot.Id,
                    FarmerId = p.Plot.FarmerId,
                    FarmerName = farmer?.FullName ?? "Unknown",
                    FarmerPhone = farmer?.PhoneNumber,
                    Area = p.Plot.Area,
                    PlantingDate = p.PlantingDate,
                    BoundaryGeoJson = p.Plot.Boundary != null ? _geoJsonWriter.Write(p.Plot.Boundary) : null,
                    SoilType = p.Plot.SoilType,
                    SoThua = p.Plot.SoThua,
                    SoTo = p.Plot.SoTo
                };
            }).ToList()
        };
    }

    private UngroupedPlotDto MapToUngroupedPlotDto(
        UngroupedPlotInfo ungrouped,
        Dictionary<Guid, RiceVariety> varietyDict,
        Dictionary<Guid, Farmer> farmerDict)
    {
        var plot = ungrouped.Plot.Plot;
        var farmer = farmerDict.GetValueOrDefault(plot.FarmerId);

        return new UngroupedPlotDto
        {
            PlotId = plot.Id,
            FarmerId = plot.FarmerId,
            FarmerName = farmer?.FullName ?? "Unknown",
            FarmerPhone = farmer?.PhoneNumber,
            RiceVarietyId = ungrouped.Plot.RiceVarietyId,
            RiceVarietyName = varietyDict.GetValueOrDefault(ungrouped.Plot.RiceVarietyId)?.VarietyName ?? "Unknown",
            PlantingDate = ungrouped.Plot.PlantingDate,
            Area = plot.Area,
            BoundaryGeoJson = plot.Boundary != null ? _geoJsonWriter.Write(plot.Boundary) : null,
            UngroupReason = ungrouped.Reason.ToString(),
            ReasonDescription = ungrouped.ReasonDescription,
            DistanceToNearestGroup = ungrouped.DistanceToNearestGroup,
            NearestGroupNumber = ungrouped.NearestGroupNumber,
            Suggestions = ungrouped.Suggestions
        };
    }
}

