using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Services;

public class GroupFormationService
{
    private readonly GeometryFactory _geometryFactory;

    public GroupFormationService()
    {
        _geometryFactory = new GeometryFactory();
    }

    public class GroupingParameters
    {
        // FIXED: Changed default from 2000m to 1000m (more realistic)
        public double ProximityThreshold { get; set; } = 100; // meters
        public int PlantingDateTolerance { get; set; } = 2; // days
        public decimal MinGroupArea { get; set; } = 5.0m; // hectares (was 15, too high)
        public decimal MaxGroupArea { get; set; } = 50.0m; // hectares
        public int MinPlotsPerGroup { get; set; } = 3; // (was 5, too high)
        public int MaxPlotsPerGroup { get; set; } = 10; // (was 15, reduced)
        public double BorderBuffer { get; set; } = 10; // meters
    }

    public class PlotClusterInfo
    {
        public Plot Plot { get; set; } = null!;
        public PlotCultivation PlotCultivation { get; set; } = null!;
        public Point Coordinate { get; set; } = null!;
        public DateTime PlantingDate { get; set; }
        public Guid RiceVarietyId { get; set; }
        public bool IsGrouped { get; set; }
        public int? ClusterNumber { get; set; }
    }

    public class ProposedGroup
    {
        public int GroupNumber { get; set; }
        public Guid RiceVarietyId { get; set; }
        public DateTime PlantingWindowStart { get; set; }
        public DateTime PlantingWindowEnd { get; set; }
        public DateTime MedianPlantingDate { get; set; }
        public List<PlotClusterInfo> Plots { get; set; } = new();
        public Point Centroid { get; set; } = null!;
        public Polygon? GroupBoundary { get; set; }
        public decimal TotalArea { get; set; }
    }

    public class UngroupedPlotInfo
    {
        public PlotClusterInfo Plot { get; set; } = null!;
        public UngroupReason Reason { get; set; }
        public string ReasonDescription { get; set; } = string.Empty;
        public double? DistanceToNearestGroup { get; set; }
        public int? NearestGroupNumber { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    public enum UngroupReason
    {
        IsolatedLocation,
        PlantingDateMismatch,
        InsufficientArea,
        GroupTooLarge,
        TooFewPlots,
        MissingCoordinate,
        AlreadyGrouped,
        NotSpatiallyCoherent,
        InvalidBoundary,
        PlotOverlap
    }

    /// <summary>
    /// Form groups using spatial and temporal clustering
    /// </summary>
    public (List<ProposedGroup> groups, List<UngroupedPlotInfo> ungrouped) FormGroups(
        List<PlotClusterInfo> eligiblePlots,
        GroupingParameters parameters)
    {
        var proposedGroups = new List<ProposedGroup>();
        var ungroupedPlots = new List<UngroupedPlotInfo>();
        int groupNumber = 1;

        // Filter out plots without coordinates
        var plotsWithCoordinates = eligiblePlots.Where(p => p.Coordinate != null).ToList();
        var plotsWithoutCoordinates = eligiblePlots.Where(p => p.Coordinate == null).ToList();

        foreach (var plot in plotsWithoutCoordinates)
        {
            ungroupedPlots.Add(new UngroupedPlotInfo
            {
                Plot = plot,
                Reason = UngroupReason.MissingCoordinate,
                ReasonDescription = "Plot boundary/coordinate not assigned",
                Suggestions = new List<string> { "Assign polygon boundary to plot before grouping" }
            });
        }

        // Group by Rice Variety first (hard constraint)
        var varietyGroups = plotsWithCoordinates.GroupBy(p => p.RiceVarietyId);

        foreach (var varietyGroup in varietyGroups)
        {
            var plotsToCluster = varietyGroup.ToList();

            // FIXED: New spatial clustering with coherence check
            var spatialClusters = SpatialClusteringWithCoherence(plotsToCluster, parameters.ProximityThreshold);

            foreach (var spatialCluster in spatialClusters)
            {
                // Within each spatial cluster, cluster by planting date
                var dateClusters = TemporalClustering(spatialCluster, parameters.PlantingDateTolerance);

                foreach (var dateCluster in dateClusters)
                {
                    var totalArea = dateCluster.Sum(p => p.Plot.Area);
                    var plotCount = dateCluster.Count;

                    // Check constraints
                    if (plotCount < parameters.MinPlotsPerGroup)
                    {
                        // Too few plots
                        foreach (var plot in dateCluster)
                        {
                            ungroupedPlots.Add(new UngroupedPlotInfo
                            {
                                Plot = plot,
                                Reason = UngroupReason.TooFewPlots,
                                ReasonDescription = $"Only {plotCount} plots in cluster (minimum {parameters.MinPlotsPerGroup} required)",
                                Suggestions = new List<string>
                                {
                                    "Assign to nearby group manually",
                                    "Reduce minimum plots per group parameter",
                                    "Create exception group"
                                }
                            });
                        }
                        continue;
                    }

                    if (totalArea < parameters.MinGroupArea)
                    {
                        // Insufficient area
                        foreach (var plot in dateCluster)
                        {
                            ungroupedPlots.Add(new UngroupedPlotInfo
                            {
                                Plot = plot,
                                Reason = UngroupReason.InsufficientArea,
                                ReasonDescription = $"Total area {totalArea:F2} ha is below minimum {parameters.MinGroupArea} ha",
                                Suggestions = new List<string>
                                {
                                    "Merge with nearby group manually",
                                    "Reduce minimum area parameter"
                                }
                            });
                        }
                        continue;
                    }

                    if (totalArea > parameters.MaxGroupArea || plotCount > parameters.MaxPlotsPerGroup)
                    {
                        // Split large cluster into multiple groups
                        var splitGroups = SplitLargeCluster(dateCluster, parameters);
                        foreach (var splitGroup in splitGroups)
                        {
                            var group = CreateProposedGroup(groupNumber++, splitGroup, varietyGroup.Key, parameters.BorderBuffer);
                            proposedGroups.Add(group);
                        }
                    }
                    else
                    {
                        // Perfect size - create group
                        var group = CreateProposedGroup(groupNumber++, dateCluster, varietyGroup.Key, parameters.BorderBuffer);
                        proposedGroups.Add(group);
                    }
                }
            }
        }

        // Analyze ungrouped plots and suggest nearest groups
        AnalyzeUngroupedPlots(ungroupedPlots, proposedGroups);

        return (proposedGroups, ungroupedPlots);
    }

    /// <summary>
    /// FIXED: Spatial clustering with coherence check to prevent chain groups
    /// </summary>
    private List<List<PlotClusterInfo>> SpatialClusteringWithCoherence(
        List<PlotClusterInfo> plots,
        double maxDistance)
    {
        var clusters = new List<List<PlotClusterInfo>>();
        var visited = new HashSet<PlotClusterInfo>();

        foreach (var plot in plots)
        {
            if (visited.Contains(plot))
                continue;

            var cluster = new List<PlotClusterInfo> { plot };
            visited.Add(plot);

            // Find all plots within maxDistance using BFS
            var queue = new Queue<PlotClusterInfo>();
            queue.Enqueue(plot);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighbor in plots)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    var distance = current.Coordinate.Distance(neighbor.Coordinate);

                    if (distance <= maxDistance)
                    {
                        // CRITICAL FIX: Check if adding this plot maintains cluster coherence
                        // The new plot must be within 2x maxDistance of ALL existing plots
                        if (IsPlotCoherentWithCluster(neighbor, cluster, maxDistance))
                        {
                            cluster.Add(neighbor);
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                        // If not coherent, it will be picked up in next iteration
                    }
                }
            }

            // Final validation: ensure cluster diameter doesn't exceed 2x threshold
            if (IsClusterSpatiallyCoherent(cluster, maxDistance))
            {
                clusters.Add(cluster);
            }
            else
            {
                // Split incoherent cluster into multiple smaller clusters
                var subClusters = SplitIncoherentCluster(cluster, maxDistance);
                clusters.AddRange(subClusters);
            }
        }

        return clusters;
    }

    /// <summary>
    /// NEW: Check if a plot is coherent with existing cluster
    /// </summary>
    private bool IsPlotCoherentWithCluster(
        PlotClusterInfo newPlot,
        List<PlotClusterInfo> cluster,
        double threshold)
    {
        // Check distance to all existing plots in cluster
        // Maximum distance to any plot should not exceed 2x threshold
        foreach (var existingPlot in cluster)
        {
            var distance = newPlot.Coordinate.Distance(existingPlot.Coordinate);
            if (distance > threshold * 2)
            {
                return false; // Too far from at least one plot
            }
        }
        return true;
    }

    /// <summary>
    /// NEW: Validate cluster spatial coherence (diameter check)
    /// </summary>
    private bool IsClusterSpatiallyCoherent(
        List<PlotClusterInfo> cluster,
        double threshold)
    {
        if (cluster.Count <= 1)
            return true;

        // Find maximum distance between any two plots
        double maxDistance = 0;
        for (int i = 0; i < cluster.Count; i++)
        {
            for (int j = i + 1; j < cluster.Count; j++)
            {
                var distance = cluster[i].Coordinate.Distance(cluster[j].Coordinate);
                if (distance > maxDistance)
                    maxDistance = distance;
            }
        }

        // Cluster diameter should not exceed 2x proximity threshold
        return maxDistance <= (threshold * 2);
    }

    /// <summary>
    /// NEW: Split incoherent cluster into smaller coherent sub-clusters
    /// </summary>
    private List<List<PlotClusterInfo>> SplitIncoherentCluster(
        List<PlotClusterInfo> cluster,
        double threshold)
    {
        var subClusters = new List<List<PlotClusterInfo>>();
        var remaining = new List<PlotClusterInfo>(cluster);

        while (remaining.Any())
        {
            var seed = remaining.First();
            var subCluster = new List<PlotClusterInfo> { seed };
            remaining.Remove(seed);

            // Add plots that maintain coherence
            var added = true;
            while (added)
            {
                added = false;
                var toAdd = new List<PlotClusterInfo>();

                foreach (var plot in remaining)
                {
                    if (IsPlotCoherentWithCluster(plot, subCluster, threshold))
                    {
                        toAdd.Add(plot);
                        added = true;
                    }
                }

                foreach (var plot in toAdd)
                {
                    subCluster.Add(plot);
                    remaining.Remove(plot);
                }
            }

            if (subCluster.Count >= 1) // Keep even single plots for later ungrouping
            {
                subClusters.Add(subCluster);
            }
        }

        return subClusters;
    }

    /// <summary>
    /// Temporal clustering by planting date
    /// </summary>
    private List<List<PlotClusterInfo>> TemporalClustering(
        List<PlotClusterInfo> plots,
        int toleranceDays)
    {
        var clusters = new List<List<PlotClusterInfo>>();

        // Sort by planting date
        var sortedPlots = plots.OrderBy(p => p.PlantingDate).ToList();

        var currentCluster = new List<PlotClusterInfo>();
        DateTime? clusterStartDate = null;

        foreach (var plot in sortedPlots)
        {
            if (clusterStartDate == null)
            {
                // Start new cluster
                clusterStartDate = plot.PlantingDate;
                currentCluster.Add(plot);
            }
            else
            {
                var daysDiff = (plot.PlantingDate - clusterStartDate.Value).Days;

                if (Math.Abs(daysDiff) <= toleranceDays)
                {
                    // Add to current cluster
                    currentCluster.Add(plot);
                }
                else
                {
                    // Start new cluster
                    if (currentCluster.Any())
                    {
                        clusters.Add(currentCluster);
                    }
                    currentCluster = new List<PlotClusterInfo> { plot };
                    clusterStartDate = plot.PlantingDate;
                }
            }
        }

        // Add last cluster
        if (currentCluster.Any())
        {
            clusters.Add(currentCluster);
        }

        return clusters;
    }

    /// <summary>
    /// Split large cluster into multiple groups
    /// </summary>
    private List<List<PlotClusterInfo>> SplitLargeCluster(
        List<PlotClusterInfo> cluster,
        GroupingParameters parameters)
    {
        var result = new List<List<PlotClusterInfo>>();

        // Simple split by area target
        var targetGroupArea = (parameters.MinGroupArea + parameters.MaxGroupArea) / 2;

        var currentGroup = new List<PlotClusterInfo>();
        var currentArea = 0m;

        foreach (var plot in cluster.OrderByDescending(p => p.Plot.Area))
        {
            if (currentArea + plot.Plot.Area <= parameters.MaxGroupArea &&
                currentGroup.Count < parameters.MaxPlotsPerGroup)
            {
                currentGroup.Add(plot);
                currentArea += plot.Plot.Area;
            }
            else
            {
                if (currentGroup.Any())
                {
                    result.Add(currentGroup);
                }
                currentGroup = new List<PlotClusterInfo> { plot };
                currentArea = plot.Plot.Area;
            }
        }

        if (currentGroup.Any())
        {
            result.Add(currentGroup);
        }

        return result;
    }

    /// <summary>
    /// Calculate smooth group boundary from plot boundaries
    /// </summary>
    private Polygon? CalculateGroupBoundary(List<PlotClusterInfo> plots, double bufferDistance)
    {
        var geometries = new List<Geometry>();

        foreach (var plot in plots)
        {
            if (plot.Plot.Boundary != null)
            {
                geometries.Add(plot.Plot.Boundary);
            }
            else if (plot.Coordinate != null)
            {
                // Create small buffer around point coordinate (5 meters)
                var point = _geometryFactory.CreatePoint(plot.Coordinate.Coordinate);
                geometries.Add(point.Buffer(5));
            }
        }

        if (!geometries.Any())
            return null;

        try
        {
            // Union all plot geometries
            var union = UnaryUnionOp.Union(geometries);

            // Add buffer for padding and create smooth boundary
            var buffered = union.Buffer(bufferDistance);

            // Apply small negative buffer to smooth sharp corners
            var smoothed = buffered.Buffer(-bufferDistance * 0.3);

            // Convert to Polygon
            if (smoothed is Polygon polygon)
            {
                return polygon;
            }
            else if (smoothed is MultiPolygon multiPolygon)
            {
                // Take convex hull to create single polygon
                return (Polygon)multiPolygon.ConvexHull();
            }
            else
            {
                // Fallback to convex hull
                return (Polygon)union.ConvexHull();
            }
        }
        catch (Exception)
        {
            // Fallback to simple union and convex hull
            try
            {
                var union = UnaryUnionOp.Union(geometries);
                if (union is Polygon polygon)
                    return polygon;
                return (Polygon)union.ConvexHull();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Create proposed group from cluster
    /// </summary>
    private ProposedGroup CreateProposedGroup(
        int groupNumber,
        List<PlotClusterInfo> plots,
        Guid riceVarietyId,
        double borderBuffer = 10)
    {
        var plantingDates = plots.Select(p => p.PlantingDate).OrderBy(d => d).ToList();
        var minDate = plantingDates.First();
        var maxDate = plantingDates.Last();
        var medianDate = plantingDates[plantingDates.Count / 2];

        // Calculate group boundary with smooth borders
        var groupBoundary = CalculateGroupBoundary(plots, borderBuffer);

        // Calculate centroid from boundary if available, otherwise from coordinates
        Point centroid;
        if (groupBoundary != null)
        {
            var boundaryCentroid = groupBoundary.Centroid;
            centroid = new Point(boundaryCentroid.X, boundaryCentroid.Y) { SRID = 4326 };
        }
        else
        {
            // Fallback: calculate from plot coordinates
            var coordinates = plots.Select(p => p.Coordinate).Where(c => c != null).ToArray();
            if (coordinates.Any())
            {
                var avgX = coordinates.Average(c => c.X);
                var avgY = coordinates.Average(c => c.Y);
                centroid = new Point(avgX, avgY) { SRID = 4326 };
            }
            else
            {
                centroid = new Point(0, 0) { SRID = 4326 };
            }
        }

        return new ProposedGroup
        {
            GroupNumber = groupNumber,
            RiceVarietyId = riceVarietyId,
            PlantingWindowStart = minDate,
            PlantingWindowEnd = maxDate,
            MedianPlantingDate = medianDate,
            Plots = plots,
            Centroid = centroid,
            GroupBoundary = groupBoundary,
            TotalArea = plots.Sum(p => p.Plot.Area)
        };
    }

    /// <summary>
    /// Analyze ungrouped plots and find nearest groups
    /// </summary>
    private void AnalyzeUngroupedPlots(
        List<UngroupedPlotInfo> ungroupedPlots,
        List<ProposedGroup> groups)
    {
        foreach (var ungrouped in ungroupedPlots)
        {
            if (ungrouped.Plot.Coordinate == null)
                continue;

            // Find nearest group with same variety
            var sameVarietyGroups = groups
                .Where(g => g.RiceVarietyId == ungrouped.Plot.RiceVarietyId)
                .ToList();

            if (sameVarietyGroups.Any())
            {
                var nearestGroup = sameVarietyGroups
                    .Select(g => new
                    {
                        Group = g,
                        Distance = ungrouped.Plot.Coordinate.Distance(g.Centroid)
                    })
                    .OrderBy(x => x.Distance)
                    .First();

                ungrouped.DistanceToNearestGroup = nearestGroup.Distance;
                ungrouped.NearestGroupNumber = nearestGroup.Group.GroupNumber;

                if (nearestGroup.Distance < 5000) // Within 5km
                {
                    ungrouped.Suggestions.Add(
                        $"Assign to Group {nearestGroup.Group.GroupNumber} manually ({nearestGroup.Distance / 1000:F2}km away)"
                    );
                }
            }

            // Add generic suggestions based on reason
            if (ungrouped.Reason == UngroupReason.IsolatedLocation)
            {
                ungrouped.Suggestions.Add("Create exception group if multiple isolated plots exist nearby");
                ungrouped.Suggestions.Add("Consider adjusting proximity threshold parameter");
            }
        }
    }
}