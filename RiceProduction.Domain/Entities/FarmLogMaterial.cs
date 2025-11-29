namespace RiceProduction.Domain.Entities;
//status: bao trùm trong farm log
public class FarmLogMaterial : BaseAuditableEntity
{
    public Guid FarmLogId { get; set; }
    public Guid MaterialId { get; set; }
    
    // Actual quantities used and recorded in this farm log entry
    public decimal ActualQuantityUsed { get; set; }
    public decimal ActualCost { get; set; }
    
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("FarmLogId")]
    public FarmLog FarmLog { get; set; } = null!;

    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}
