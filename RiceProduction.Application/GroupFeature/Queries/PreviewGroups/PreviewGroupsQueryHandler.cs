using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Application.Common.Services;
using RiceProduction.Domain.Entities;
using System.Text.Json;
using static RiceProduction.Application.Common.Services.GroupFormationService;

namespace RiceProduction.Application.GroupFeature.Queries.PreviewGroups;

public class PreviewGroupsQueryHandler : IRequestHandler<PreviewGroupsQuery, Result<PreviewGroupsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PreviewGroupsQueryHandler> _logger;
    private readonly GroupFormationService _groupFormationService;
    private readonly GroupNameGenerationService _groupNameService;
    private readonly GeoJsonWriter _geoJsonWriter;

    public PreviewGroupsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<PreviewGroupsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
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
            // Verify cluster exists
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<PreviewGroupsResponse>.Failure($"Cluster {request.ClusterId} not found");
            }

            // Verify season exists
            var season = await _unitOfWork.Repository<Season>()
                .FindAsync(s => s.Id == request.SeasonId);

            if (season == null)
            {
                return Result<PreviewGroupsResponse>.Failure($"Season {request.SeasonId} not found");
            }

            // Build grouping parameters
            var parameters = new GroupingParameters
            {
                ProximityThreshold = request.ProximityThreshold ?? 100,
                PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
                MinGroupArea = request.MinGroupArea ?? 5.0m,
                MaxGroupArea = request.MaxGroupArea ?? 15,
                MinPlotsPerGroup = request.MinPlotsPerGroup ?? 3,
                MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 10
            };

            // Get all farmers in cluster
            var farmers = await _unitOfWork.FarmerRepository
                .ListAsync(f => f.ClusterId == request.ClusterId);
            var farmersList = farmers.ToList();
            var farmerIds = farmersList.Select(f => f.Id).ToList();

            // Get all plots for these farmers
            var plots = await _unitOfWork.Repository<Plot>()
                .ListAsync(p => farmerIds.Contains(p.FarmerId));
            var plotsList = plots.ToList();
            var plotIds = plotsList.Select(p => p.Id).ToList();

            // Get plot cultivations for this season
            var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(pc => plotIds.Contains(pc.PlotId) &&
                                pc.SeasonId == request.SeasonId);
            var cultivationsList = plotCultivations.ToList();

            if (!cultivationsList.Any())
            {
                return Result<PreviewGroupsResponse>.Failure(
                    "No plot cultivations found for this season. " +
                    "Farmers must select rice varieties before forming groups.");
            }

            // Check for plots that are already grouped for THIS SEASON
            // Business rule: A plot can belong to multiple groups, but only one group per season
            var alreadyGroupedPlots = new HashSet<Guid>();
            foreach (var plot in plotsList)
            {
                var isGroupedForSeason = await _unitOfWork.PlotRepository.IsPlotAssignedToGroupForSeasonAsync(plot.Id, request.SeasonId, cancellationToken);
                if (isGroupedForSeason)
                {
                    alreadyGroupedPlots.Add(plot.Id);
                }
            }

            // Build PlotClusterInfo list
            var plotClusterInfos = new List<PlotClusterInfo>();

            foreach (var cultivation in cultivationsList)
            {
                var plot = plotsList.FirstOrDefault(p => p.Id == cultivation.PlotId);
                if (plot == null) continue;

                // Skip plots already grouped for THIS SEASON
                if (alreadyGroupedPlots.Contains(plot.Id))
                    continue;

                var coordinate = plot.Coordinate ?? (plot.Boundary != null ? plot.Boundary.Centroid : null);

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
                    "No eligible plots found for grouping. " +
                    "Ensure plots have coordinates/boundaries and aren't already grouped.");
            }

            // Run grouping algorithm
            var (proposedGroups, ungroupedPlots) = _groupFormationService.FormGroups(
                plotClusterInfos,
                parameters
            );

            // Get rice varieties for display
            var varietyIds = plotClusterInfos.Select(p => p.RiceVarietyId).Distinct().ToList();
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                .ListAsync(rv => varietyIds.Contains(rv.Id));
            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

            // Get farmers for display
            var farmerDict = farmersList.ToDictionary(f => f.Id);

            // Get supervisor area capacity from SystemSetting
            var supervisorAreaCapacitySetting = await _unitOfWork.Repository<SystemSetting>()
                .FindAsync(s => s.SettingKey == "SupervisorMaxAreaCapacity");
            
            decimal? supervisorMaxArea = null;
            if (supervisorAreaCapacitySetting != null && 
                decimal.TryParse(supervisorAreaCapacitySetting.SettingValue, out var areaCapacity))
            {
                supervisorMaxArea = areaCapacity;
            }

            var activeSupervisors = await _unitOfWork.SupervisorRepository
                .ListAsync(s => s.IsActive && s.ClusterId == request.ClusterId);
            
            var supervisorsList = activeSupervisors.ToList();
            var supervisorIds = supervisorsList.Select(s => s.Id).ToList();

            // Get current groups assigned to supervisors for this season
            var existingGroups = await _unitOfWork.Repository<Group>()
                .ListAsync(g => supervisorIds.Contains(g.SupervisorId!.Value) && 
                               g.YearSeason!.Year == request.Year &&
                               g.YearSeason.SeasonId == request.SeasonId);

            // Calculate current workload per supervisor
            var supervisorWorkload = existingGroups
                .GroupBy(g => g.SupervisorId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => new { 
                        GroupCount = g.Count(), 
                        TotalArea = g.Sum(x => x.TotalArea ?? 0) 
                    }
                );

            // Build available supervisors list
            var availableSupervisors = new List<SupervisorForAssignmentDto>();
            var supervisorsForAssignment = new List<Supervisor>();

            foreach (var supervisor in supervisorsList)
            {
                var workload = supervisorWorkload.GetValueOrDefault(supervisor.Id);
                var currentArea = workload?.TotalArea ?? 0;
                var currentGroups = workload?.GroupCount ?? 0;

                bool isAvailable = true;
                string? unavailableReason = null;

                // Check area capacity if set (only constraint)
                decimal? remainingArea = null;
                if (supervisorMaxArea.HasValue)
                {
                    remainingArea = supervisorMaxArea.Value - currentArea;
                    if (remainingArea <= 0)
                    {
                        isAvailable = false;
                        unavailableReason = "Area capacity reached";
                    }
                }
                else
                {
                    // If no area capacity is set, supervisor is always available
                    isAvailable = true;
                }

                var supervisorDto = new SupervisorForAssignmentDto
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
                };

                availableSupervisors.Add(supervisorDto);
                
                if (isAvailable)
                {
                    supervisorsForAssignment.Add(supervisor);
                }
            }

            // Sort supervisors by available area capacity (for assignment)
            supervisorsForAssignment = supervisorsForAssignment
                .OrderBy(s => supervisorWorkload.GetValueOrDefault(s.Id)?.TotalArea ?? 0)
                .ToList();

            // Build response
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

                PreviewGroups = proposedGroups.Select((g, index) => 
                {
                    // Assign supervisor for preview
                    Guid? supervisorId = null;
                    string? supervisorName = null;
                    
                    if (supervisorsForAssignment.Any())
                    {
                        var supervisor = supervisorsForAssignment[index % supervisorsForAssignment.Count];
                        supervisorId = supervisor.Id;
                        supervisorName = supervisor.FullName;
                    }
                    
                    // Generate group name
                    var groupName = _groupNameService.GenerateGroupName(
                        cluster.ClusterName,
                        season.SeasonName,
                        request.Year,
                        varietyDict.GetValueOrDefault(g.RiceVarietyId)?.VarietyName ?? "Unknown",
                        g.GroupNumber
                    );
                    
                    return new PreviewGroupDto
                    {
                        GroupNumber = g.GroupNumber,
                        GroupName = groupName,
                        RiceVarietyId = g.RiceVarietyId,
                        RiceVarietyName = varietyDict.GetValueOrDefault(g.RiceVarietyId)?.VarietyName ?? "Unknown",
                        SupervisorId = supervisorId,
                        SupervisorName = supervisorName,
                        PlantingWindowStart = g.PlantingWindowStart,
                        PlantingWindowEnd = g.PlantingWindowEnd,
                        MedianPlantingDate = g.MedianPlantingDate,
                        PlotCount = g.Plots.Count,
                        TotalArea = g.TotalArea,
                        CentroidLat = g.Centroid.Y,
                        CentroidLng = g.Centroid.X,
                        //GroupBoundaryGeoJson = g.GroupBoundary != null ? _geoJsonWriter.Write(g.GroupBoundary) : null,
                        GroupBoundaryGeoJson = null,
                        PlotIds = g.Plots.Select(p => p.Plot.Id).ToList(),
                        Plots = g.Plots.Select(p => new PlotInGroupDto
                        {
                            PlotId = p.Plot.Id,
                            FarmerId = p.Plot.FarmerId,
                            FarmerName = farmerDict.GetValueOrDefault(p.Plot.FarmerId)?.FullName ?? "Unknown",
                            FarmerPhone = farmerDict.GetValueOrDefault(p.Plot.FarmerId)?.PhoneNumber,
                            Area = p.Plot.Area,
                            PlantingDate = p.PlantingDate,
                            BoundaryGeoJson = p.Plot.Boundary != null ? _geoJsonWriter.Write(p.Plot.Boundary) : null,
                            SoilType = p.Plot.SoilType,
                            SoThua = p.Plot.SoThua,
                            SoTo = p.Plot.SoTo
                        }).ToList()
                    };
                }).ToList(),
                UngroupedPlots = ungroupedPlots.Select(u => new UngroupedPlotDto
                {
                    PlotId = u.Plot.Plot.Id,
                    FarmerId = u.Plot.Plot.FarmerId,
                    FarmerName = farmerDict.GetValueOrDefault(u.Plot.Plot.FarmerId)?.FullName ?? "Unknown",
                    FarmerPhone = farmerDict.GetValueOrDefault(u.Plot.Plot.FarmerId)?.PhoneNumber,
                    RiceVarietyId = u.Plot.RiceVarietyId,
                    RiceVarietyName = varietyDict.GetValueOrDefault(u.Plot.RiceVarietyId)?.VarietyName ?? "Unknown",
                    PlantingDate = u.Plot.PlantingDate,
                    Area = u.Plot.Plot.Area,
                    BoundaryGeoJson = u.Plot.Plot.Boundary != null ? _geoJsonWriter.Write(u.Plot.Plot.Boundary) : null,
                    UngroupReason = u.Reason.ToString(),
                    ReasonDescription = u.ReasonDescription,
                    DistanceToNearestGroup = u.DistanceToNearestGroup,
                    NearestGroupNumber = u.NearestGroupNumber,
                    Suggestions = u.Suggestions
                }).ToList()
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
            return Result<PreviewGroupsResponse>.Failure($"Error previewing groups: {ex.Message}");
        }
    }
}

