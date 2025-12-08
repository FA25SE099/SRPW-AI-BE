namespace RiceProduction.Domain.Entities;
//status: active, pending polygon
public class Plot : BaseAuditableEntity
{

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
}
