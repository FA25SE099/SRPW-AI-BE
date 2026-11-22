namespace RiceProduction.Domain.Entities;

public class Material : BaseAuditableEntity
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public MaterialType Type { get; set; }

    public decimal? AmmountPerMaterial { get; set; }

    [Required]
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    public string? Description { get; set; }
    public List<string>? imgUrls { get; set; }

    [MaxLength(255)]
    public string? Manufacturer { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPartition { get; set; } = false;

    // Navigation properties
    public ICollection<MaterialPrice> MaterialPrices { get; set; } = new List<MaterialPrice>();
    public ICollection<StandardPlanTaskMaterial> StandardPlanTaskMaterials { get; set; } = new List<StandardPlanTaskMaterial>();
    public ICollection<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials { get; set; } = new List<ProductionPlanTaskMaterial>();
    public ICollection<CultivationTaskMaterial> CultivationTaskMaterials { get; set; } = new List<CultivationTaskMaterial>();
    public ICollection<MaterialDistribution> MaterialDistributions { get; set; } = new List<MaterialDistribution>();

    public decimal EffectiveAmountPerMaterial => AmmountPerMaterial is > 0 ? AmmountPerMaterial.Value : 1M;

    public decimal CalculatePackagesNeeded(decimal totalQuantityNeeded) =>
        IsPartition
            ? totalQuantityNeeded / EffectiveAmountPerMaterial
            : Math.Ceiling(totalQuantityNeeded / EffectiveAmountPerMaterial);

    public decimal CalculateTotalCost(decimal totalQuantityNeeded, decimal pricePerMaterial) =>
        CalculatePackagesNeeded(totalQuantityNeeded) * pricePerMaterial;
}
