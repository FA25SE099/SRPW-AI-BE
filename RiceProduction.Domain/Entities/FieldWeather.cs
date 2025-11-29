namespace RiceProduction.Domain.Entities;
//status: alert triggered or not triggered vì lượng mưa
public class FieldWeather : BaseAuditableEntity
{
    public Guid? ClusterId { get; set; }


    [Required]
    public DateTime RecordedAt { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? Temperature { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? Humidity { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? Rainfall { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? WindSpeed { get; set; }

    [MaxLength(100)]
    public string? Conditions { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ForecastData { get; set; }

    public bool AlertTriggered { get; set; } = false;

    // Navigation properties
    [ForeignKey("ClusterId")]
    public Cluster? Cluster { get; set; }

}