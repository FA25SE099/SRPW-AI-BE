namespace RiceProduction.Domain.Entities;

public class Plot : BaseAuditableEntity
{
    public const double MetersPerDegree = 111319.9;
    public const int Wgs84Srid = 4326;

    [Required]
    public Guid FarmerId { get; set; }

    /// <summary>
    /// Polygon boundary - can be null when plot is first created, supervisor will assign later
    /// </summary>
    [Column(TypeName = "geometry(Polygon,4326)")]
    public Polygon? Boundary { get; set; }

    public int? SoThua { get; set; }
    public int? SoTo { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Area { get; set; }

    [MaxLength(100)]
    public string? SoilType { get; set; }

    /// <summary>
    /// Centroid point for distance calculations
    /// </summary>
    [Column(TypeName = "geometry(Point,4326)")]
    public Point? Coordinate { get; set; }

    public PlotStatus Status { get; set; } = PlotStatus.Active;

    // Navigation properties
    [ForeignKey("FarmerId")]
    public Farmer Farmer { get; set; } = null!;

    public ICollection<GroupPlot> GroupPlots { get; set; } = new List<GroupPlot>();
    public ICollection<PlotCultivation> PlotCultivations { get; set; } = new List<PlotCultivation>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public bool HasValidBoundary() => Boundary is { IsValid: true, IsEmpty: false };

    public static decimal CalculatePolygonAreaInHectares(Polygon polygon, int? decimalPlaces = null)
    {
        var areaInSquareMeters = polygon.Area * MetersPerDegree * MetersPerDegree;
        var hectares = (decimal)(areaInSquareMeters / 10000.0);

        return decimalPlaces.HasValue
            ? Math.Round(hectares, decimalPlaces.Value)
            : hectares;
    }

    public bool IsBoundaryWithinTolerance(
        Polygon polygon,
        decimal tolerancePercent,
        out decimal drawnAreaHa,
        out decimal differencePercent)
    {
        drawnAreaHa = CalculatePolygonAreaInHectares(polygon);
        differencePercent = Area > 0
            ? Math.Abs(drawnAreaHa - Area) / Area * 100
            : 0;

        return differencePercent <= tolerancePercent;
    }

    public void AssignBoundary(Polygon polygon)
    {
        Boundary = polygon;
        if (polygon.IsValid && !polygon.IsEmpty)
        {
            Coordinate = polygon.Centroid;
            Coordinate.SRID = Wgs84Srid;
        }

        Status = PlotStatus.Active;
    }

    public void MarkPendingPolygon(Polygon defaultBoundary)
    {
        Boundary = defaultBoundary;
        Status = PlotStatus.PendingPolygon;
    }
}
