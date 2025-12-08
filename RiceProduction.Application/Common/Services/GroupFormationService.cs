using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Index.Strtree;
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
        public double ProximityThreshold { get; set; } = 100; // meters
        public int PlantingDateTolerance { get; set; } = 2; // days
        public decimal MinGroupArea { get; set; } = 5.0m; // hectares
        public decimal MaxGroupArea { get; set; } = 50.0m; // hectares
        public int MinPlotsPerGroup { get; set; } = 3;
        public int MaxPlotsPerGroup { get; set; } = 10;
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
    /// Form groups using DBSCAN spatial clustering
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

            // Use DBSCAN clustering
            var spatialClusters = SpatialClusteringDBSCAN(
                plotsToCluster,
                parameters.ProximityThreshold,
                parameters.MinPlotsPerGroup
            );

            foreach (var spatialCluster in spatialClusters)
            {
                // Filter out clusters that don't meet minimum
                if (spatialCluster.Count < parameters.MinPlotsPerGroup)
                {
                    foreach (var plot in spatialCluster)
                    {
                        ungroupedPlots.Add(new UngroupedPlotInfo
                        {
                            Plot = plot,
                            Reason = UngroupReason.TooFewPlots,
                            ReasonDescription = $"Only {spatialCluster.Count} plots in spatial cluster (minimum {parameters.MinPlotsPerGroup} required)",
                            Suggestions = new List<string>
                            {
                                "Assign to nearby group manually",
                                "Reduce minimum plots per group parameter"
                            }
                        });
                    }
                    continue;
                }

                // Within each spatial cluster, cluster by planting date
                var dateClusters = TemporalClustering(spatialCluster, parameters.PlantingDateTolerance);

                foreach (var dateCluster in dateClusters)
                {
                    var totalArea = dateCluster.Sum(p => p.Plot.Area);
                    var plotCount = dateCluster.Count;

                    // Check constraints
                    if (plotCount < parameters.MinPlotsPerGroup)
                    {
                        foreach (var plot in dateCluster)
                        {
                            ungroupedPlots.Add(new UngroupedPlotInfo
                            {
                                Plot = plot,
                                Reason = UngroupReason.PlantingDateMismatch,
                                ReasonDescription = $"Planting date differs too much from cluster median (only {plotCount} plots remain)",
                                Suggestions = new List<string>
                                {
                                    "Increase planting date tolerance",
                                    "Assign to nearby group manually"
                                }
                            });
                        }
                        continue;
                    }

                    if (totalArea < parameters.MinGroupArea)
                    {
                        foreach (var plot in dateCluster)
                        {
                            ungroupedPlots.Add(new UngroupedPlotInfo
                            {
                                Plot = plot,
                                Reason = UngroupReason.InsufficientArea,
                                ReasonDescription = $"Total area {totalArea:F2} ha below minimum {parameters.MinGroupArea} ha",
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
    /// DBSCAN clustering using NetTopologySuite's STRtree
    /// </summary>
    private List<List<PlotClusterInfo>> SpatialClusteringDBSCAN(
        List<PlotClusterInfo> plots,
        double eps,
        int minPoints)
    {
        if (plots.Count == 0)
            return new List<List<PlotClusterInfo>>();

        // Convert eps to degrees for spatial index envelope
        var epsInDegrees = eps / 111000.0;

        // Build spatial index for efficient neighbor queries
        var spatialIndex = new STRtree<PlotClusterInfo>();
        foreach (var plot in plots)
        {
            var envelope = new Envelope(
                plot.Coordinate.X - epsInDegrees,
                plot.Coordinate.X + epsInDegrees,
                plot.Coordinate.Y - epsInDegrees,
                plot.Coordinate.Y + epsInDegrees
            );
            spatialIndex.Insert(envelope, plot);
        }
        spatialIndex.Build();

        // DBSCAN state
        var visited = new HashSet<PlotClusterInfo>();
        var clustered = new HashSet<PlotClusterInfo>();
        var clusters = new List<List<PlotClusterInfo>>();

        foreach (var plot in plots)
        {
            if (visited.Contains(plot))
                continue;

            visited.Add(plot);

            // Find neighbors within eps distance
            var neighbors = GetNeighbors(plot, plots, spatialIndex, eps);

            if (neighbors.Count < minPoints)
            {
                // Mark as noise (will be ungrouped later)
                continue;
            }

            // Start a new cluster
            var cluster = new List<PlotClusterInfo>();
            ExpandCluster(plot, neighbors, cluster, visited, clustered, spatialIndex, plots, eps, minPoints);

            // Validate cluster coherence (diameter check)
            if (IsClusterSpatiallyCoherent(cluster, eps))
            {
                clusters.Add(cluster);
            }
            else
            {
                // Split incoherent cluster
                Console.WriteLine($"🔧 Splitting incoherent cluster of {cluster.Count} plots");
                var subClusters = SplitByDiameter(cluster, eps);
                Console.WriteLine($"   → Created {subClusters.Count} sub-clusters");
                clusters.AddRange(subClusters);
            }
        }

        return clusters;
    }

    /// <summary>
    /// Get neighbors within eps distance using spatial index
    /// </summary>
    private List<PlotClusterInfo> GetNeighbors(
        PlotClusterInfo plot,
        List<PlotClusterInfo> allPlots,
        STRtree<PlotClusterInfo> spatialIndex,
        double eps)
    {
        // Convert eps from meters to degrees (approximate for envelope)
        // At Vietnam's latitude (~10-20°), 1 degree ≈ 111 km
        var epsInDegrees = eps / 111000.0;

        var envelope = new Envelope(
            plot.Coordinate.X - epsInDegrees,
            plot.Coordinate.X + epsInDegrees,
            plot.Coordinate.Y - epsInDegrees,
            plot.Coordinate.Y + epsInDegrees
        );

        var candidates = spatialIndex.Query(envelope).Cast<PlotClusterInfo>();
        var neighbors = new List<PlotClusterInfo>();

        foreach (var candidate in candidates)
        {
            if (candidate == plot)
                continue;

            // Use geodesic distance (accurate for lat/lon)
            var distance = CalculateDistanceInMeters(plot.Coordinate, candidate.Coordinate);
            if (distance <= eps)
            {
                neighbors.Add(candidate);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Calculate geodesic distance between two points in meters
    /// Uses Haversine formula for lat/lon coordinates
    /// </summary>
    private double CalculateDistanceInMeters(Point point1, Point point2)
    {
        const double EarthRadiusMeters = 6371000.0;

        var lat1 = point1.Y * Math.PI / 180.0;
        var lat2 = point2.Y * Math.PI / 180.0;
        var deltaLat = (point2.Y - point1.Y) * Math.PI / 180.0;
        var deltaLon = (point2.X - point1.X) * Math.PI / 180.0;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    /// <summary>
    /// Expand cluster using DBSCAN algorithm
    /// </summary>
    private void ExpandCluster(
        PlotClusterInfo plot,
        List<PlotClusterInfo> neighbors,
        List<PlotClusterInfo> cluster,
        HashSet<PlotClusterInfo> visited,
        HashSet<PlotClusterInfo> clustered,
        STRtree<PlotClusterInfo> spatialIndex,
        List<PlotClusterInfo> allPlots,
        double eps,
        int minPoints)
    {
        cluster.Add(plot);
        clustered.Add(plot);

        var queue = new Queue<PlotClusterInfo>(neighbors);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Contains(current))
            {
                visited.Add(current);
                var currentNeighbors = GetNeighbors(current, allPlots, spatialIndex, eps);

                if (currentNeighbors.Count >= minPoints)
                {
                    foreach (var neighbor in currentNeighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (!clustered.Contains(current))
            {
                cluster.Add(current);
                clustered.Add(current);
            }
        }
    }

    /// <summary>
    /// Validate cluster diameter (matches PostGIS logic)
    /// </summary>
    private bool IsClusterSpatiallyCoherent(List<PlotClusterInfo> cluster, double eps)
    {
        if (cluster.Count <= 1)
            return true;

        double maxDiameter = 0;
        for (int i = 0; i < cluster.Count; i++)
        {
            for (int j = i + 1; j < cluster.Count; j++)
            {
                // Use geodesic distance in meters
                var distance = CalculateDistanceInMeters(cluster[i].Coordinate, cluster[j].Coordinate);
                maxDiameter = Math.Max(maxDiameter, distance);
            }
        }

        // Cluster diameter must not exceed 2x threshold
        var isCoherent = maxDiameter <= (eps * 2);

        if (!isCoherent)
        {
            Console.WriteLine($"⚠️ Cluster incoherent: {cluster.Count} plots, diameter {maxDiameter:F1}m > {eps * 2}m");
        }

        return isCoherent;
    }

    /// <summary>
    /// Split incoherent cluster into smaller valid sub-clusters
    /// </summary>
    private List<List<PlotClusterInfo>> SplitByDiameter(List<PlotClusterInfo> cluster, double eps)
    {
        var subClusters = new List<List<PlotClusterInfo>>();
        var remaining = new HashSet<PlotClusterInfo>(cluster);

        while (remaining.Any())
        {
            var seed = remaining
                .Select(p => new
                {
                    Plot = p,
                    NeighborCount = remaining.Count(other =>
                        CalculateDistanceInMeters(p.Coordinate, other.Coordinate) <= eps)
                })
                .OrderByDescending(x => x.NeighborCount)
                .First()
                .Plot;

            var subCluster = new List<PlotClusterInfo> { seed };
            remaining.Remove(seed);

            bool added = true;
            while (added)
            {
                added = false;
                var candidates = remaining
                    .Where(p => {
                        // Check if adding this plot keeps ALL pairwise distances <= 2*eps
                        foreach (var existing in subCluster)
                        {
                            var dist = CalculateDistanceInMeters(existing.Coordinate, p.Coordinate);
                            if (dist > eps * 2)
                                return false;
                        }
                        return true;
                    })
                    .OrderBy(p => {
                        var centroid = CalculateCentroid(subCluster);
                        return CalculateDistanceInMeters(p.Coordinate, centroid);
                    })
                    .ToList();

                if (candidates.Any())
                {
                    var toAdd = candidates.First();
                    subCluster.Add(toAdd);
                    remaining.Remove(toAdd);
                    added = true;
                }
            }

            if (IsClusterSpatiallyCoherent(subCluster, eps))
            {
                subClusters.Add(subCluster);
            }
        }

        return subClusters;
    }

    /// <summary>
    /// Calculate centroid of cluster
    /// </summary>
    private Point CalculateCentroid(List<PlotClusterInfo> cluster)
    {
        if (cluster.Count == 0)
            return null!;

        if (cluster.Count == 1)
            return cluster[0].Coordinate;

        var avgX = cluster.Average(p => p.Coordinate.X);
        var avgY = cluster.Average(p => p.Coordinate.Y);
        return new Point(avgX, avgY) { SRID = 4326 };
    }

    /// <summary>
    /// Temporal clustering by planting date
    /// </summary>
    private List<List<PlotClusterInfo>> TemporalClustering(
        List<PlotClusterInfo> plots,
        int toleranceDays)
    {
        var clusters = new List<List<PlotClusterInfo>>();
        var sortedPlots = plots.OrderBy(p => p.PlantingDate).ToList();

        var currentCluster = new List<PlotClusterInfo>();
        DateTime? clusterStartDate = null;

        foreach (var plot in sortedPlots)
        {
            if (clusterStartDate == null)
            {
                clusterStartDate = plot.PlantingDate;
                currentCluster.Add(plot);
            }
            else
            {
                var daysDiff = (plot.PlantingDate - clusterStartDate.Value).Days;

                if (Math.Abs(daysDiff) <= toleranceDays)
                {
                    currentCluster.Add(plot);
                }
                else
                {
                    if (currentCluster.Any())
                    {
                        clusters.Add(currentCluster);
                    }
                    currentCluster = new List<PlotClusterInfo> { plot };
                    clusterStartDate = plot.PlantingDate;
                }
            }
        }

        if (currentCluster.Any())
        {
            clusters.Add(currentCluster);
        }

        return clusters;
    }

    /// <summary>
    /// Split large cluster into multiple groups
    /// </summary>
    /// <summary>
    /// Split large cluster while maintaining spatial coherence
    /// </summary>
    private List<List<PlotClusterInfo>> SplitLargeCluster(
        List<PlotClusterInfo> cluster,
        GroupingParameters parameters)
    {
        var result = new List<List<PlotClusterInfo>>();

        // Create a working list we can remove from
        var remainingPlots = new List<PlotClusterInfo>(cluster);

        while (remainingPlots.Any())
        {
            var currentGroup = new List<PlotClusterInfo>();
            var currentArea = 0m;

            // 1. Pick a "Seed" plot to start the new group.
            // We pick the plot that is furthest West (Min X) or North (Max Y) 
            // to start 'eating' the cluster from one edge, rather than the middle.
            var seed = remainingPlots.OrderBy(p => p.Coordinate.X).First();

            currentGroup.Add(seed);
            currentArea += seed.Plot.Area;
            remainingPlots.Remove(seed);

            // 2. Grow the group by adding the nearest available neighbors
            bool groupIsFull = false;

            while (!groupIsFull && remainingPlots.Any())
            {
                // Find the centroid of the current growing group
                var groupCentroid = CalculateCentroid(currentGroup);

                // Find the nearest ungrouped plot to this centroid
                var nearestNeighbor = remainingPlots
                    .Select(p => new {
                        Plot = p,
                        Distance = CalculateDistanceInMeters(p.Coordinate, groupCentroid)
                    })
                    .OrderBy(x => x.Distance)
                    .First()
                    .Plot;

                // Check constraints
                bool fitsArea = (currentArea + nearestNeighbor.Plot.Area) <= parameters.MaxGroupArea;
                bool fitsCount = currentGroup.Count < parameters.MaxPlotsPerGroup;

                if (fitsArea && fitsCount)
                {
                    currentGroup.Add(nearestNeighbor);
                    currentArea += nearestNeighbor.Plot.Area;
                    remainingPlots.Remove(nearestNeighbor);
                }
                else
                {
                    // Group is full (either by area or count)
                    groupIsFull = true;
                }
            }

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
                var point = _geometryFactory.CreatePoint(plot.Coordinate.Coordinate);
                geometries.Add(point.Buffer(5));
            }
        }

        if (!geometries.Any())
            return null;

        try
        {
            var union = UnaryUnionOp.Union(geometries);
            var buffered = union.Buffer(bufferDistance);
            var smoothed = buffered.Buffer(-bufferDistance * 0.3);

            if (smoothed is Polygon polygon)
            {
                return polygon;
            }
            else if (smoothed is MultiPolygon multiPolygon)
            {
                return (Polygon)multiPolygon.ConvexHull();
            }
            else
            {
                return (Polygon)union.ConvexHull();
            }
        }
        catch
        {
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

        var groupBoundary = CalculateGroupBoundary(plots, borderBuffer);

        Point centroid;
        if (groupBoundary != null)
        {
            var boundaryCentroid = groupBoundary.Centroid;
            centroid = new Point(boundaryCentroid.X, boundaryCentroid.Y) { SRID = 4326 };
        }
        else
        {
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

            var sameVarietyGroups = groups
                .Where(g => g.RiceVarietyId == ungrouped.Plot.RiceVarietyId)
                .ToList();

            if (sameVarietyGroups.Any())
            {
                var nearestGroup = sameVarietyGroups
                    .Select(g => new
                    {
                        Group = g,
                        Distance = CalculateDistanceInMeters(ungrouped.Plot.Coordinate, g.Centroid)
                    })
                    .OrderBy(x => x.Distance)
                    .First();

                ungrouped.DistanceToNearestGroup = nearestGroup.Distance;
                ungrouped.NearestGroupNumber = nearestGroup.Group.GroupNumber;

                if (nearestGroup.Distance < 5000)
                {
                    ungrouped.Suggestions.Add(
                        $"Assign to Group {nearestGroup.Group.GroupNumber} manually ({nearestGroup.Distance:F0}m away)"
                    );
                }
            }

            if (ungrouped.Reason == UngroupReason.IsolatedLocation || ungrouped.Reason == UngroupReason.TooFewPlots)
            {
                ungrouped.Suggestions.Add("Create exception group if multiple isolated plots exist nearby");
                ungrouped.Suggestions.Add("Consider adjusting proximity threshold parameter");
            }
        }
    }
}