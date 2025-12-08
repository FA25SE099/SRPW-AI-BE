//using Microsoft.Extensions.Logging;
//using NetTopologySuite.IO;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Application.Common.Models;
//using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
//using RiceProduction.Domain.Entities;

//namespace RiceProduction.Application.GroupFeature.Queries.PreviewGroups;

///// <summary>
///// PostGIS-optimized preview groups query handler
///// Uses database-side spatial operations for improved accuracy and performance
///// </summary>
//public class PreviewGroupsPostGISQueryHandler : IRequestHandler<PreviewGroupsQuery, Result<PreviewGroupsResponse>>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly ILogger<PreviewGroupsPostGISQueryHandler> _logger;
//    private readonly IPostGISGroupFormationService _postGISService;
//    private readonly GeoJsonWriter _geoJsonWriter;

//    public PreviewGroupsPostGISQueryHandler(
//        IUnitOfWork unitOfWork,
//        ILogger<PreviewGroupsPostGISQueryHandler> logger,
//        IPostGISGroupFormationService postGISService)
//    {
//        _unitOfWork = unitOfWork;
//        _logger = logger;
//        _postGISService = postGISService;
//        _geoJsonWriter = new GeoJsonWriter();
//    }

//    public async Task<Result<PreviewGroupsResponse>> Handle(
//        PreviewGroupsQuery request,
//        CancellationToken cancellationToken)
//    {
//        try
//        {
//            _logger.LogInformation("Starting PostGIS-based group preview for cluster {ClusterId}, season {SeasonId}, year {Year}",
//                request.ClusterId, request.SeasonId, request.Year);

//            // Verify cluster exists
//            var cluster = await _unitOfWork.Repository<Cluster>()
//                .FindAsync(c => c.Id == request.ClusterId);

//            if (cluster == null)
//            {
//                return Result<PreviewGroupsResponse>.Failure($"Cluster {request.ClusterId} not found");
//            }

//            // Verify season exists
//            var season = await _unitOfWork.Repository<Season>()
//                .FindAsync(s => s.Id == request.SeasonId);

//            if (season == null)
//            {
//                return Result<PreviewGroupsResponse>.Failure($"Season {request.SeasonId} not found");
//            }

//            // Build grouping parameters
//            var parameters = new PostGISGroupingParameters
//            {
//                ProximityThreshold = request.ProximityThreshold ?? 100, // 100m default
//                PlantingDateTolerance = request.PlantingDateTolerance ?? 2,
//                MinGroupArea = request.MinGroupArea ?? 5.0m,
//                MaxGroupArea = request.MaxGroupArea ?? 50.0m,
//                MinPlotsPerGroup = request.MinPlotsPerGroup ?? 3,
//                MaxPlotsPerGroup = request.MaxPlotsPerGroup ?? 10,
//                BorderBuffer = 10
//            };

//            _logger.LogInformation("Using PostGIS spatial clustering with parameters: Proximity={Proximity}m, DateTolerance={DateTolerance}days",
//                parameters.ProximityThreshold, parameters.PlantingDateTolerance);

//            // Run PostGIS-based grouping algorithm (preview mode) with cluster and season filters
//            var groupingResult = await _postGISService.FormGroupsAsync(
//                parameters, 
//                request.ClusterId, 
//                request.SeasonId, 
//                cancellationToken);

//            _logger.LogInformation("PostGIS preview completed: {GroupCount} groups formed, {UngroupedCount} plots ungrouped",
//                groupingResult.Groups.Count, groupingResult.UngroupedPlots.Count);

//            // Get farmers in cluster for display
//            var farmers = await _unitOfWork.FarmerRepository
//                .ListAsync(f => f.ClusterId == request.ClusterId);
//            var farmersList = farmers.ToList();

//            // Get all plot IDs from the grouping result
//            var allPlotIds = groupingResult.Groups
//                .SelectMany(g => g.PlotIds)
//                .Concat(groupingResult.UngroupedPlots.Select(u => u.PlotId))
//                .Distinct()
//                .ToList();

//            // Load plots for display
//            var plots = await _unitOfWork.Repository<Plot>()
//                .ListAsync(p => allPlotIds.Contains(p.Id));
//            var plotDict = plots.ToDictionary(p => p.Id);

//            // Load plot cultivations for display
//            var cultivations = await _unitOfWork.Repository<PlotCultivation>()
//                .ListAsync(pc => allPlotIds.Contains(pc.PlotId) && pc.SeasonId == request.SeasonId);
//            var cultivationDict = cultivations.ToDictionary(pc => pc.PlotId);

//            // Get rice varieties for display
//            var varietyIds = groupingResult.Groups.Select(g => g.RiceVarietyId)
//                .Concat(groupingResult.UngroupedPlots.Select(u => u.RiceVarietyId))
//                .Distinct()
//                .ToList();
//            var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
//                .ListAsync(rv => varietyIds.Contains(rv.Id));
//            var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

//            // Get farmers dict for lookups
//            var farmerDict = farmersList.ToDictionary(f => f.Id);

//            // Build response
//            var response = new PreviewGroupsResponse
//            {
//                ClusterId = request.ClusterId,
//                SeasonId = request.SeasonId,
//                Year = request.Year,
//                Parameters = new GroupingParametersDto
//                {
//                    ProximityThreshold = parameters.ProximityThreshold,
//                    PlantingDateTolerance = parameters.PlantingDateTolerance,
//                    MinGroupArea = parameters.MinGroupArea,
//                    MaxGroupArea = parameters.MaxGroupArea,
//                    MinPlotsPerGroup = parameters.MinPlotsPerGroup,
//                    MaxPlotsPerGroup = parameters.MaxPlotsPerGroup
//                },
//                Summary = new PreviewSummary
//                {
//                    TotalEligiblePlots = allPlotIds.Count,
//                    PlotsGrouped = groupingResult.Groups.Sum(g => g.PlotCount),
//                    UngroupedPlots = groupingResult.UngroupedPlots.Count,
//                    GroupsToBeFormed = groupingResult.Groups.Count,
//                    EstimatedTotalArea = groupingResult.Groups.Sum(g => g.TotalArea)
//                },
//                PreviewGroups = groupingResult.Groups.Select(g => new PreviewGroupDto
//                {
//                    GroupNumber = g.GroupNumber,
//                    RiceVarietyId = g.RiceVarietyId,
//                    RiceVarietyName = varietyDict.GetValueOrDefault(g.RiceVarietyId)?.VarietyName ?? "Unknown",
//                    PlantingWindowStart = g.PlantingWindowStart,
//                    PlantingWindowEnd = g.PlantingWindowEnd,
//                    MedianPlantingDate = g.MedianPlantingDate,
//                    PlotCount = g.PlotCount,
//                    TotalArea = g.TotalArea,
//                    CentroidLat = g.GroupCentroid?.Y ?? 0,
//                    CentroidLng = g.GroupCentroid?.X ?? 0,
//                    GroupBoundaryGeoJson = g.GroupBoundary != null ? _geoJsonWriter.Write(g.GroupBoundary) : null,
//                    PlotIds = g.PlotIds,
//                    Plots = g.PlotIds.Select(plotId =>
//                    {
//                        var plot = plotDict.GetValueOrDefault(plotId);
//                        var cultivation = cultivationDict.GetValueOrDefault(plotId);
                        
//                        if (plot == null) return null;

//                        return new PlotInGroupDto
//                        {
//                            PlotId = plot.Id,
//                            FarmerId = plot.FarmerId,
//                            FarmerName = farmerDict.GetValueOrDefault(plot.FarmerId)?.FullName ?? "Unknown",
//                            FarmerPhone = farmerDict.GetValueOrDefault(plot.FarmerId)?.PhoneNumber,
//                            Area = plot.Area,
//                            PlantingDate = cultivation?.PlantingDate ?? DateTime.MinValue,
//                            BoundaryGeoJson = plot.Boundary != null ? _geoJsonWriter.Write(plot.Boundary) : null,
//                            SoilType = plot.SoilType,
//                            SoThua = plot.SoThua,
//                            SoTo = plot.SoTo
//                        };
//                    })
//                    .Where(p => p != null)
//                    .Cast<PlotInGroupDto>()
//                    .ToList()
//                }).ToList(),
//                UngroupedPlots = groupingResult.UngroupedPlots.Select(u =>
//                {
//                    var plot = plotDict.GetValueOrDefault(u.PlotId);
//                    if (plot == null) return null;

//                    return new UngroupedPlotDto
//                    {
//                        PlotId = u.PlotId,
//                        FarmerId = plot.FarmerId,
//                        FarmerName = farmerDict.GetValueOrDefault(plot.FarmerId)?.FullName ?? "Unknown",
//                        FarmerPhone = farmerDict.GetValueOrDefault(plot.FarmerId)?.PhoneNumber,
//                        RiceVarietyId = u.RiceVarietyId,
//                        RiceVarietyName = varietyDict.GetValueOrDefault(u.RiceVarietyId)?.VarietyName ?? "Unknown",
//                        PlantingDate = u.PlantingDate,
//                        Area = u.Area,
//                        BoundaryGeoJson = plot.Boundary != null ? _geoJsonWriter.Write(plot.Boundary) : null,
//                        UngroupReason = u.UngroupedReason,
//                        ReasonDescription = GetReasonDescription(u.UngroupedReason, parameters),
//                        DistanceToNearestGroup = u.DistanceToNearestGroup,
//                        NearestGroupNumber = u.NearestGroupNumber,
//                        Suggestions = GetSuggestions(u.UngroupedReason, u.DistanceToNearestGroup, u.NearestGroupNumber)
//                    };
//                })
//                .Where(u => u != null)
//                .Cast<UngroupedPlotDto>()
//                .ToList()
//            };

//            // Add detailed breakdown to logs
//            if (groupingResult.UngroupedPlots.Any())
//            {
//                var ungroupedSummary = groupingResult.UngroupedPlots
//                    .GroupBy(u => u.UngroupedReason)
//                    .Select(g => $"{g.Key}: {g.Count()} plots")
//                    .ToList();

//                _logger.LogInformation("Ungrouped plots breakdown: {UngroupedBreakdown}",
//                    string.Join(", ", ungroupedSummary));
//            }

//            _logger.LogInformation(
//                "Preview groups for cluster {ClusterId}, season {SeasonId}: {GroupCount} groups, {PlotCount} plots grouped, {UngroupedCount} ungrouped",
//                request.ClusterId, request.SeasonId, response.PreviewGroups.Count,
//                response.Summary.PlotsGrouped, response.Summary.UngroupedPlots);

//            return Result<PreviewGroupsResponse>.Success(response);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error previewing groups for cluster {ClusterId} using PostGIS", request.ClusterId);
//            return Result<PreviewGroupsResponse>.Failure($"Error previewing groups: {ex.Message}");
//        }
//    }

//    private string GetReasonDescription(string reason, PostGISGroupingParameters parameters)
//    {
//        return reason switch
//        {
//            "IsolatedLocation" => $"No nearby plots within {parameters.ProximityThreshold}m proximity threshold",
//            "NotSpatiallyCoherent" => "Would create an elongated chain group (fails spatial coherence check)",
//            "TooFewPlots" => $"Cluster has fewer than {parameters.MinPlotsPerGroup} plots (minimum required)",
//            "InsufficientArea" => $"Total cluster area is below {parameters.MinGroupArea} hectares",
//            "GroupTooLarge" => $"Would exceed maximum area of {parameters.MaxGroupArea} hectares",
//            "TooManyPlots" => $"Would exceed maximum of {parameters.MaxPlotsPerGroup} plots per group",
//            "ConstraintViolation" => "Does not meet one or more grouping constraints",
//            _ => "Unknown reason"
//        };
//    }

//    private List<string> GetSuggestions(string reason, double? distanceToNearest, int? nearestGroupNumber)
//    {
//        var suggestions = new List<string>();

//        switch (reason)
//        {
//            case "IsolatedLocation":
//                if (distanceToNearest.HasValue && distanceToNearest.Value < 5000)
//                {
//                    suggestions.Add($"Consider manually assigning to Group {nearestGroupNumber} ({distanceToNearest.Value / 1000:F2}km away)");
//                }
//                suggestions.Add("Increase proximity threshold if appropriate for this region");
//                suggestions.Add("Create an exception group for isolated plots");
//                break;

//            case "NotSpatiallyCoherent":
//                suggestions.Add("This plot would create a chain group - manually review spatial arrangement");
//                suggestions.Add("Consider splitting the cluster into multiple smaller groups");
//                break;

//            case "TooFewPlots":
//                suggestions.Add("Reduce minimum plots per group parameter");
//                suggestions.Add("Manually merge with nearby small cluster");
//                suggestions.Add("Create exception group with supervisor approval");
//                break;

//            case "InsufficientArea":
//                suggestions.Add("Reduce minimum group area parameter");
//                suggestions.Add("Merge with nearby group manually");
//                break;

//            case "GroupTooLarge":
//            case "TooManyPlots":
//                suggestions.Add("The cluster will be automatically split into multiple groups");
//                suggestions.Add("Adjust maximum area/plot count parameters if needed");
//                break;

//            default:
//                suggestions.Add("Review grouping parameters");
//                suggestions.Add("Contact administrator for manual assignment");
//                break;
//        }

//        return suggestions;
//    }
//}

