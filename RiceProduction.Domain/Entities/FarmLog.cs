namespace RiceProduction.Domain.Entities;
//status: về mặt ngữ nghĩa thì không có status, nhưng có được gửi bởi ai và được ai xác nhận,
//gían tiếp trạng thái là đã được xác nhận hay chưa được xác nhận
public class FarmLog : BaseAuditableEntity
{
    [Required]
    public Guid CultivationTaskId { get; set; }

    [Required]
    public Guid PlotCultivationId { get; set; }

    [Required]
    public Guid LoggedBy { get; set; }

    [Required]
    public DateTime LoggedDate { get; set; }

    // Work completion
    public string? WorkDescription { get; set; }

    public int CompletionPercentage { get; set; } = 100;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualAreaCovered { get; set; }

    // Material usage (JSON: [{material_id, quantity, cost}])
    [Column(TypeName = "jsonb")]
    public string? ActualMaterialJson { get; set; }

    // Service execution
    [Column(TypeName = "decimal(12,2)")]
    public decimal? ServiceCost { get; set; }

    public string? ServiceNotes { get; set; }

    // Proof and verification
    public string[]? PhotoUrls { get; set; }

    [MaxLength(255)]
    public string? WeatherConditions { get; set; }

    public string? InterruptionReason { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    // Navigation properties
    [ForeignKey("CultivationTaskId")]
    public CultivationTask CultivationTask { get; set; } = null!;

    [ForeignKey("PlotCultivationId")]
    public PlotCultivation PlotCultivation { get; set; } = null!;

    [ForeignKey("LoggedBy")]
    public Farmer Logger { get; set; } = null!;

    [ForeignKey("VerifiedBy")]
    public Supervisor? Verifier { get; set; }

    public ICollection<FarmLogMaterial> FarmLogMaterials { get; set; } = new List<FarmLogMaterial>();
}
