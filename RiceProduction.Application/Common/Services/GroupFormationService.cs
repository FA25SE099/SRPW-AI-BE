using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Services;

public class GroupFormationService
{
    public class GroupingParameters
    {
        public double ProximityThreshold { get; set; } = 2000; // meters
        public int PlantingDateTolerance { get; set; } = 2; // days
        public decimal MinGroupArea { get; set; } = 15.0m; // hectares
        public decimal MaxGroupArea { get; set; } = 50.0m; // hectares
        public int MinPlotsPerGroup { get; set; } = 5;
        public int MaxPlotsPerGroup { get; set; } = 15;
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
        AlreadyGrouped
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

            // Spatial clustering using simple distance-based approach
            var spatialClusters = SpatialClustering(plotsToCluster, parameters.ProximityThreshold);

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
                            var group = CreateProposedGroup(groupNumber++, splitGroup, varietyGroup.Key);
                            proposedGroups.Add(group);
                        }
                    }
                    else
                    {
                        // Perfect size - create group
                        var group = CreateProposedGroup(groupNumber++, dateCluster, varietyGroup.Key);
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
    /// Spatial clustering using simple distance-based grouping
    /// </summary>
    private List<List<PlotClusterInfo>> SpatialClustering(
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

            // Find all plots within maxDistance
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
                        cluster.Add(neighbor);
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
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
    /// Create proposed group from cluster
    /// </summary>
    private ProposedGroup CreateProposedGroup(
        int groupNumber,
        List<PlotClusterInfo> plots,
        Guid riceVarietyId)
    {
        var plantingDates = plots.Select(p => p.PlantingDate).OrderBy(d => d).ToList();
        var minDate = plantingDates.First();
        var maxDate = plantingDates.Last();
        var medianDate = plantingDates[plantingDates.Count / 2];

        // Calculate centroid
        var coordinates = plots.Select(p => p.Coordinate).ToArray();
        var avgX = coordinates.Average(c => c.X);
        var avgY = coordinates.Average(c => c.Y);
        var centroid = new Point(avgX, avgY) { SRID = 4326 };

        // Calculate group boundary (union of all plot boundaries)
        Polygon? groupBoundary = null;
        var plotBoundaries = plots
            .Select(p => p.Plot.Boundary)
            .Where(b => b != null)
            .ToList();

        if (plotBoundaries.Any())
        {
            Geometry union = plotBoundaries.First()!;
            foreach (var boundary in plotBoundaries.Skip(1))
            {
                union = union.Union(boundary!);
            }

            if (union is Polygon polygon)
            {
                groupBoundary = polygon;
            }
            else if (union is MultiPolygon multiPolygon)
            {
                // Take the convex hull to create a single polygon
                groupBoundary = (Polygon)multiPolygon.ConvexHull();
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

