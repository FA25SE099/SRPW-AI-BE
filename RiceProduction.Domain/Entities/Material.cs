namespace RiceProduction.Domain.Entities;

public class Material : BaseAuditableEntity
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public MaterialType Type { get; set; }

    public decimal? AmmountPerMaterial { get; set; }//lieu luong cua 1 vat tu vd: 400
    [Required]
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;//don vi tinh vd: ml, kg
    public string? Description { get; set; }

    [MaxLength(255)]
    public string? Manufacturer { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<MaterialPrice> MaterialPrices { get; set; } = new List<MaterialPrice>();
    public ICollection<StandardPlanTaskMaterial> StandardPlanTaskMaterials { get; set; } = new List<StandardPlanTaskMaterial>();
    public ICollection<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials { get; set; } = new List<ProductionPlanTaskMaterial>();
    public ICollection<CultivationTaskMaterial> CultivationTaskMaterials { get; set; } = new List<CultivationTaskMaterial>();

}
