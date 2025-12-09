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
    private readonly GeoJsonWriter _geoJsonWriter;

    public PreviewGroupsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<PreviewGroupsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _groupFormationService = new GroupFormationService();
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
                Summary = new PreviewSummary
                {
                    TotalEligiblePlots = plotClusterInfos.Count,
                    PlotsGrouped = proposedGroups.Sum(g => g.Plots.Count),
                    UngroupedPlots = ungroupedPlots.Count,
                    GroupsToBeFormed = proposedGroups.Count,
                    EstimatedTotalArea = proposedGroups.Sum(g => g.TotalArea)
                },
                PreviewGroups = proposedGroups.Select(g => new PreviewGroupDto
                {
                    GroupNumber = g.GroupNumber,
                    RiceVarietyId = g.RiceVarietyId,
                    RiceVarietyName = varietyDict.GetValueOrDefault(g.RiceVarietyId)?.VarietyName ?? "Unknown",
                    PlantingWindowStart = g.PlantingWindowStart,
                    PlantingWindowEnd = g.PlantingWindowEnd,
                    MedianPlantingDate = g.MedianPlantingDate,
                    PlotCount = g.Plots.Count,
                    TotalArea = g.TotalArea,
                    CentroidLat = g.Centroid.Y,
                    CentroidLng = g.Centroid.X,
                    GroupBoundaryGeoJson = g.GroupBoundary != null ? _geoJsonWriter.Write(g.GroupBoundary) : null,
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
                "Preview groups for cluster {ClusterId}, season {SeasonId}: {GroupCount} groups, {PlotCount} plots grouped, {UngroupedCount} ungrouped",
                request.ClusterId, request.SeasonId, response.PreviewGroups.Count,
                response.Summary.PlotsGrouped, response.Summary.UngroupedPlots);

            return Result<PreviewGroupsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing groups for cluster {ClusterId}", request.ClusterId);
            return Result<PreviewGroupsResponse>.Failure($"Error previewing groups: {ex.Message}");
        }
    }
}

