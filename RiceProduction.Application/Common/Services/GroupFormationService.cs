using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Index.Strtree;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Services;

public class GroupFormationService
{
    private const double MetersPerDegree = 111000.0;
    private const double EarthRadiusMeters = 6371000.0;
    private const int Wgs84Srid = 4326;

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

        AddUngroupedPlots(
            ungroupedPlots,
            plotsWithoutCoordinates,
            UngroupReason.MissingCoordinate,
            "Chưa gán tọa độ/ranh giới thửa đất",
            "Gán ranh giới đa giác cho thửa đất trước khi tạo nhóm");

        // Group by Rice Variety first (hard constraint)
        var varietyGroups = plotsWithCoordinates.GroupBy(p => p.RiceVarietyId);

        foreach (var varietyGroup in varietyGroups)
        {
            var plotsToCluster = varietyGroup.ToList();

            var spatialClusters = SpatialClusteringDBSCAN(
                plotsToCluster,
                parameters.ProximityThreshold,
                parameters.MinPlotsPerGroup);

            foreach (var spatialCluster in spatialClusters)
            {
                if (spatialCluster.Count < parameters.MinPlotsPerGroup)
                {
                    AddUngroupedPlots(
                        ungroupedPlots,
                        spatialCluster,
                        UngroupReason.TooFewPlots,
                        $"Chỉ có {spatialCluster.Count} thửa trong cụm không gian (tối thiểu yêu cầu {parameters.MinPlotsPerGroup} thửa)",
                        "Gán thủ công vào nhóm gần đó",
                        "Giảm thông số số thửa tối thiểu mỗi nhóm");
                    continue;
                }

                // Within each spatial cluster, cluster by planting date
                var dateClusters = TemporalClustering(spatialCluster, parameters.PlantingDateTolerance);

                foreach (var dateCluster in dateClusters)
                {
                    var totalArea = dateCluster.Sum(p => p.Plot.Area);
                    var plotCount = dateCluster.Count;

                    if (plotCount < parameters.MinPlotsPerGroup)
                    {
                        AddUngroupedPlots(
                            ungroupedPlots,
                            dateCluster,
                            UngroupReason.PlantingDateMismatch,
                            $"Ngày gieo sạ khác biệt quá nhiều so với trung vị cụm (chỉ còn {plotCount} thửa)",
                            "Tăng dung sai ngày gieo sạ",
                            "Gán thủ công vào nhóm gần đó");
                        continue;
                    }

                    if (totalArea < parameters.MinGroupArea)
                    {
                        AddUngroupedPlots(
                            ungroupedPlots,
                            dateCluster,
                            UngroupReason.InsufficientArea,
                            $"Tổng diện tích {totalArea:F2} ha thấp hơn tối thiểu {parameters.MinGroupArea} ha",
                            "Gộp thủ công với nhóm gần đó",
                            "Giảm thông số diện tích tối thiểu");
                        continue;
                    }

                    var groupsToCreate = totalArea > parameters.MaxGroupArea || plotCount > parameters.MaxPlotsPerGroup
                        ? SplitLargeCluster(dateCluster, parameters)
                        : new List<List<PlotClusterInfo>> { dateCluster };

                    foreach (var groupPlots in groupsToCreate)
                    {
                        proposedGroups.Add(CreateProposedGroup(groupNumber++, groupPlots, varietyGroup.Key, parameters.BorderBuffer));
                    }
                }
            }
        }

        // Analyze ungrouped plots and suggest nearest groups
        AnalyzeUngroupedPlots(ungroupedPlots, proposedGroups);

        return (proposedGroups, ungroupedPlots);
    }

    private static void AddUngroupedPlots(
        List<UngroupedPlotInfo> target,
        IEnumerable<PlotClusterInfo> plots,
        UngroupReason reason,
        string reasonDescription,
        params string[] suggestions)
    {
        foreach (var plot in plots)
        {
            target.Add(new UngroupedPlotInfo
            {
                Plot = plot,
                Reason = reason,
                ReasonDescription = reasonDescription,
                Suggestions = new List<string>(suggestions)
            });
        }
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

        // Approximate meters-to-degrees conversion for spatial index envelope (~111 km/degree at Vietnam's latitude)
        var epsInDegrees = eps / MetersPerDegree;

        var spatialIndex = new STRtree<PlotClusterInfo>();
        foreach (var plot in plots)
        {
            spatialIndex.Insert(CreateSearchEnvelope(plot.Coordinate, epsInDegrees), plot);
        }
        spatialIndex.Build();

        var visited = new HashSet<PlotClusterInfo>();
        var clustered = new HashSet<PlotClusterInfo>();
        var clusters = new List<List<PlotClusterInfo>>();

        foreach (var plot in plots)
        {
            if (!visited.Add(plot))
                continue;

            var neighbors = GetNeighbors(plot, spatialIndex, eps, epsInDegrees);
            if (neighbors.Count < minPoints)
                continue; // Noise (will be ungrouped later)

            var cluster = new List<PlotClusterInfo>();
            ExpandCluster(plot, neighbors, cluster, visited, clustered, spatialIndex, eps, epsInDegrees, minPoints);

            if (IsClusterSpatiallyCoherent(cluster, eps))
            {
                clusters.Add(cluster);
            }
            else
            {
                Console.WriteLine($"🔧 Splitting incoherent cluster of {cluster.Count} plots");
                var subClusters = SplitByDiameter(cluster, eps);
                Console.WriteLine($"   → Created {subClusters.Count} sub-clusters");
                clusters.AddRange(subClusters);
            }
        }

        return clusters;
    }

    private static Envelope CreateSearchEnvelope(Point coordinate, double epsInDegrees) =>
        new(
            coordinate.X - epsInDegrees,
            coordinate.X + epsInDegrees,
            coordinate.Y - epsInDegrees,
            coordinate.Y + epsInDegrees);

    /// <summary>
    /// Get neighbors within eps distance using spatial index
    /// </summary>
    private List<PlotClusterInfo> GetNeighbors(
        PlotClusterInfo plot,
        STRtree<PlotClusterInfo> spatialIndex,
        double eps,
        double epsInDegrees)
    {
        var candidates = spatialIndex.Query(CreateSearchEnvelope(plot.Coordinate, epsInDegrees));
        var neighbors = new List<PlotClusterInfo>();

        foreach (var candidate in candidates)
        {
            if (candidate == plot)
                continue;

            if (CalculateDistanceInMeters(plot.Coordinate, candidate.Coordinate) <= eps)
            {
                neighbors.Add(candidate);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Calculate geodesic distance between two points in meters using the Haversine formula
    /// </summary>
    private static double CalculateDistanceInMeters(Point point1, Point point2)
    {
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
        double eps,
        double epsInDegrees,
        int minPoints)
    {
        cluster.Add(plot);
        clustered.Add(plot);

        var queue = new Queue<PlotClusterInfo>(neighbors);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (visited.Add(current))
            {
                var currentNeighbors = GetNeighbors(current, spatialIndex, eps, epsInDegrees);
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

            if (clustered.Add(current))
            {
                cluster.Add(current);
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
        var maxDiameter = eps * 2;

        while (remaining.Count > 0)
        {
            var seed = remaining
                .OrderByDescending(p => remaining.Count(other =>
                    CalculateDistanceInMeters(p.Coordinate, other.Coordinate) <= eps))
                .First();

            var subCluster = new List<PlotClusterInfo> { seed };
            remaining.Remove(seed);

            while (true)
            {
                var centroid = CalculateCentroid(subCluster);
                var toAdd = remaining
                    .Where(p => subCluster.All(existing =>
                        CalculateDistanceInMeters(existing.Coordinate, p.Coordinate) <= maxDiameter))
                    .OrderBy(p => CalculateDistanceInMeters(p.Coordinate, centroid))
                    .FirstOrDefault();

                if (toAdd == null)
                    break;

                subCluster.Add(toAdd);
                remaining.Remove(toAdd);
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
    private static Point CalculateCentroid(List<PlotClusterInfo> cluster)
    {
        if (cluster.Count == 0)
            return null!;

        if (cluster.Count == 1)
            return cluster[0].Coordinate;

        var avgX = cluster.Average(p => p.Coordinate.X);
        var avgY = cluster.Average(p => p.Coordinate.Y);
        return new Point(avgX, avgY) { SRID = Wgs84Srid };
    }

    /// <summary>
    /// Temporal clustering by planting date
    /// </summary>
    private static List<List<PlotClusterInfo>> TemporalClustering(
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
                continue;
            }

            var daysDiff = (plot.PlantingDate - clusterStartDate.Value).Days;
            if (Math.Abs(daysDiff) <= toleranceDays)
            {
                currentCluster.Add(plot);
                continue;
            }

            if (currentCluster.Count > 0)
            {
                clusters.Add(currentCluster);
            }

            currentCluster = new List<PlotClusterInfo> { plot };
            clusterStartDate = plot.PlantingDate;
        }

        if (currentCluster.Count > 0)
        {
            clusters.Add(currentCluster);
        }

        return clusters;
    }

    /// <summary>
    /// Split large cluster while maintaining spatial coherence
    /// </summary>
    private List<List<PlotClusterInfo>> SplitLargeCluster(
        List<PlotClusterInfo> cluster,
        GroupingParameters parameters)
    {
        var result = new List<List<PlotClusterInfo>>();
        var remainingPlots = new List<PlotClusterInfo>(cluster);

        while (remainingPlots.Count > 0)
        {
            // Pick the westernmost plot as seed to grow from an edge rather than the middle
            var seed = remainingPlots.OrderBy(p => p.Coordinate.X).First();
            var currentGroup = new List<PlotClusterInfo> { seed };
            var currentArea = seed.Plot.Area;
            remainingPlots.Remove(seed);

            // Grow the group by adding the nearest available neighbors within constraints
            while (remainingPlots.Count > 0 && currentGroup.Count < parameters.MaxPlotsPerGroup)
            {
                var groupCentroid = CalculateCentroid(currentGroup);
                var nearestNeighbor = remainingPlots
                    .OrderBy(p => CalculateDistanceInMeters(p.Coordinate, groupCentroid))
                    .First();

                if (currentArea + nearestNeighbor.Plot.Area > parameters.MaxGroupArea)
                    break;

                currentGroup.Add(nearestNeighbor);
                currentArea += nearestNeighbor.Plot.Area;
                remainingPlots.Remove(nearestNeighbor);
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

        if (geometries.Count == 0)
            return null;

        try
        {
            var union = UnaryUnionOp.Union(geometries);
            var buffered = union.Buffer(bufferDistance);
            var smoothed = buffered.Buffer(-bufferDistance * 0.3);

            return smoothed switch
            {
                Polygon polygon => polygon,
                MultiPolygon multiPolygon => (Polygon)multiPolygon.ConvexHull(),
                _ => (Polygon)union.ConvexHull()
            };
        }
        catch
        {
            try
            {
                var union = UnaryUnionOp.Union(geometries);
                return union is Polygon polygon ? polygon : (Polygon)union.ConvexHull();
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
        var groupBoundary = CalculateGroupBoundary(plots, borderBuffer);

        return new ProposedGroup
        {
            GroupNumber = groupNumber,
            RiceVarietyId = riceVarietyId,
            PlantingWindowStart = plantingDates[0],
            PlantingWindowEnd = plantingDates[^1],
            MedianPlantingDate = plantingDates[plantingDates.Count / 2],
            Plots = plots,
            Centroid = CalculateGroupCentroid(groupBoundary, plots),
            GroupBoundary = groupBoundary,
            TotalArea = plots.Sum(p => p.Plot.Area)
        };
    }

    private static Point CalculateGroupCentroid(Polygon? groupBoundary, List<PlotClusterInfo> plots)
    {
        if (groupBoundary != null)
        {
            var boundaryCentroid = groupBoundary.Centroid;
            return new Point(boundaryCentroid.X, boundaryCentroid.Y) { SRID = Wgs84Srid };
        }

        var coordinates = plots.Select(p => p.Coordinate).Where(c => c != null).ToArray();
        if (coordinates.Length == 0)
            return new Point(0, 0) { SRID = Wgs84Srid };

        var avgX = coordinates.Average(c => c.X);
        var avgY = coordinates.Average(c => c.Y);
        return new Point(avgX, avgY) { SRID = Wgs84Srid };
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

            if (sameVarietyGroups.Count > 0)
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
                        $"Gán thủ công vào Nhóm {nearestGroup.Group.GroupNumber} (cách {nearestGroup.Distance:F0}m)");
                }
            }

            if (ungrouped.Reason is UngroupReason.IsolatedLocation or UngroupReason.TooFewPlots)
            {
                ungrouped.Suggestions.Add("Tạo nhóm ngoại lệ nếu có nhiều thửa cô lập gần đó");
                ungrouped.Suggestions.Add("Xem xét điều chỉnh thông số ngưỡng khoảng cách");
            }
        }
    }
}