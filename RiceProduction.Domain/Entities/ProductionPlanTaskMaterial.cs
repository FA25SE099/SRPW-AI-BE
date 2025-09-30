namespace RiceProduction.Domain.Entities;

public class ProductionPlanTaskMaterial : BaseAuditableEntity
{
    [Required]
    public Guid ProductionPlanTaskId { get; set; }
    
    [Required]
    public Guid MaterialId { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(10,3)")]
    public decimal QuantityPerHa { get; set; }
    
    [Column(TypeName = "decimal(12,2)")]
    public decimal? EstimatedAmount { get; set; }
    
    // Navigation properties
    [ForeignKey("ProductionPlanTaskId")]
    public ProductionPlanTask ProductionPlanTask { get; set; } = null!;
    
    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}