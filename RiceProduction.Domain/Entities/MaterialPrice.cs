namespace RiceProduction.Domain.Entities;
//status: về mặt ngữ nghĩa flow thì giá có đang có hiệu lực hay
//không tùy thuộc vào khoảng thời gian ValidFrom và ValidTo
public class MaterialPrice : BaseAuditableEntity
{
    [Required]
    public Guid MaterialId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePerMaterial { get; set; }

    [Required]
    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    // Navigation properties
    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
}
