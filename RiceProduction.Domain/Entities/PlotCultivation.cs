namespace RiceProduction.Domain.Entities;

public class PlotCultivation : BaseAuditableEntity
{
    [Required]
    public Guid PlotId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    public Guid RiceVarietyId { get; set; }

    [Required]
    public DateTime PlantingDate { get; set; }
    public decimal? Area { get; set; }


    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualYield { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ExpectedYield { get; set; }

    public CultivationStatus Status { get; set; } = CultivationStatus.Planned;

    [ForeignKey("PlotId")]
    public Plot Plot { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public Season Season { get; set; } = null!;

    [ForeignKey("RiceVarietyId")]
    public RiceVariety RiceVariety { get; set; } = null!;

    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();
    public ICollection<CultivationTask> CultivationTasks { get; set; } = new List<CultivationTask>();
    public ICollection<CultivationVersion> CultivationVersions { get; set; } = new List<CultivationVersion>();
    public ICollection<FarmLog> FarmLogs { get; set; } = new List<FarmLog>();
}
