namespace RiceProduction.Domain.Entities;

public class CultivationTaskMaterial : BaseAuditableEntity
{
    public Guid CultivationTaskId { get; set; }
    public Guid MaterialId { get; set; }
    
    // Actual quantities used during execution
    public decimal ActualQuantity { get; set; }
    public decimal ActualCost { get; set; }
    
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("CultivationTaskId")]
    public CultivationTask CultivationTask { get; set; } = null!;

    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}