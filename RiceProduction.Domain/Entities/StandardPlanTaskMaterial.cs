namespace RiceProduction.Domain.Entities;
//status: không có trạng thái
public class StandardPlanTaskMaterial : BaseAuditableEntity
{
    [Required]
    public Guid StandardPlanTaskId { get; set; }

    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,3)")]
    public decimal QuantityPerHa { get; set; }

    // Navigation properties
    [ForeignKey("StandardPlanTaskId")]
    public StandardPlanTask StandardPlanTask { get; set; } = null!;

    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}