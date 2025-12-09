namespace RiceProduction.Domain.Entities;

public class Cluster : BaseAuditableEntity
{
    [Required]
    [MaxLength(255)]
    public string ClusterName { get; set; } = string.Empty;

    public Guid? ClusterManagerId { get; set; }
    public Guid? AgronomyExpertId { get; set; }

    [Column(TypeName = "geometry(Polygon,4326)")]
    public Polygon? Boundary { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Area { get; set; }

    // Navigation properties
    [ForeignKey("ClusterManagerId")]
    public ClusterManager? ClusterManager { get; set; }
    [ForeignKey("AgronomyExpertId")]
    public AgronomyExpert? AgronomyExpert { get; set; }
    
    public ICollection<Supervisor> SupervisorsInCluster { get; set; } = new List<Supervisor>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<FieldWeather> WeatherData { get; set; } = new List<FieldWeather>();
}
