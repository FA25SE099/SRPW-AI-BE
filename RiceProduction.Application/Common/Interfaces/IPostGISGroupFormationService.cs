using NetTopologySuite.Geometries;

namespace RiceProduction.Application.Common.Interfaces;

/// <summary>
/// PostGIS-based group formation service interface
/// </summary>
public interface IPostGISGroupFormationService
{
    Task<PostGISGroupFormationResult> FormGroupsAsync(
        PostGISGroupingParameters parameters,
        Guid? clusterId = null,
        Guid? seasonId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Parameters for PostGIS spatial group formation
/// </summary>
public class PostGISGroupingParameters
{
    public double ProximityThreshold { get; set; } = 100; // meters
    public int PlantingDateTolerance { get; set; } = 2; // days
    public decimal MinGroupArea { get; set; } = 5.0m; // hectares
    public decimal MaxGroupArea { get; set; } = 50.0m; // hectares
    public int MinPlotsPerGroup { get; set; } = 3;
    public int MaxPlotsPerGroup { get; set; } = 10;
    public double BorderBuffer { get; set; } = 10; // meters
}

/// <summary>
/// Proposed group from PostGIS clustering
/// </summary>
public class PostGISProposedGroup
{
    public int GroupNumber { get; set; }
    public Guid RiceVarietyId { get; set; }
    public DateTime PlantingWindowStart { get; set; }
    public DateTime PlantingWindowEnd { get; set; }
    public DateTime MedianPlantingDate { get; set; }
    public List<Guid> PlotIds { get; set; } = new();
    public List<Guid> CultivationIds { get; set; } = new();
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
    public Polygon? GroupBoundary { get; set; }
    public Point? GroupCentroid { get; set; }
}

/// <summary>
/// Information about plots that could not be grouped
/// </summary>
public class PostGISUngroupedPlotInfo
{
    public Guid PlotId { get; set; }
    public Guid CultivationId { get; set; }
    public Guid RiceVarietyId { get; set; }
    public DateTime PlantingDate { get; set; }
    public Point? Centroid { get; set; }
    public decimal Area { get; set; }
    public string UngroupedReason { get; set; } = string.Empty;
    public int? NearestGroupNumber { get; set; }
    public double? DistanceToNearestGroup { get; set; }
}

/// <summary>
/// Result of PostGIS group formation operation
/// </summary>
public class PostGISGroupFormationResult
{
    public List<PostGISProposedGroup> Groups { get; set; } = new();
    public List<PostGISUngroupedPlotInfo> UngroupedPlots { get; set; } = new();
}

