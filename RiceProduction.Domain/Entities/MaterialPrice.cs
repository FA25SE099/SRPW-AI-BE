namespace RiceProduction.Domain.Entities;

public class MaterialPrice : BaseAuditableEntity
{
    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePerUnit { get; set; }

    [Required]
    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    // Navigation properties
    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}
