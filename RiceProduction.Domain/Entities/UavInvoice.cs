namespace RiceProduction.Domain.Entities;

public class UavInvoice : BaseAuditableEntity
{
    [Required]
    public Guid UavVendorId { get; set; }

    public Guid? UavServiceOrderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    public DateTime InvoiceDate { get; set; }

    // Amounts
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalArea { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal RatePerHa { get; set; }

    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Tax { get; set; } = 0;

    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    // Payment
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public DateTime? DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public Guid? PaidBy { get; set; }

    public string? Notes { get; set; }

    public string[]? AttachmentUrls { get; set; }

    // Navigation properties
    [ForeignKey("UavVendorId")]
    public UavVendor UavVendor { get; set; } = null!;

    [ForeignKey("UavServiceOrderId")]
    public UavServiceOrder? UavServiceOrder { get; set; }

}