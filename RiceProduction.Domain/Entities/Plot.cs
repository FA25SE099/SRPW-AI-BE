namespace RiceProduction.Domain.Entities;

public class Plot : BaseAuditableEntity
{

    [Required]
    public Guid FarmerId { get; set; }

    public Guid? GroupId { get; set; }

    [Required]
    [Column(TypeName = "geometry(Polygon,4326)")]
    public Polygon Boundary { get; set; } = null!;
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

    [ForeignKey("GroupId")]
    public Group? Group { get; set; }

    public ICollection<PlotCultivation> PlotCultivations { get; set; } = new List<PlotCultivation>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
