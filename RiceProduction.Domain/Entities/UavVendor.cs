namespace RiceProduction.Domain.Entities;

public class UavVendor : ApplicationUser
{
    /// <summary>
    /// Company or vendor name
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string VendorName { get; set; } = string.Empty;

    /// <summary>
    /// Business registration number
    /// </summary>
    [MaxLength(100)]
    public string? BusinessRegistrationNumber { get; set; }

    /// <summary>
    /// Service rate per hectare
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal ServiceRatePerHa { get; set; }

    public int CompletedServices { get; set; } = 0;

    /// <summary>
    /// UAV fleet size
    /// </summary>
    public int FleetSize { get; set; } = 1;

    /// <summary>
    /// Service coverage area in km radius
    /// </summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal? ServiceRadius { get; set; }

    /// <summary>
    /// Equipment specifications (JSON)
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? EquipmentSpecs { get; set; }


    [Column(TypeName = "jsonb")]
    public string? OperatingSchedule { get; set; }

    // Navigation properties
    public ICollection<CultivationTask> AssignedTasks { get; set; } = new List<CultivationTask>();
    public ICollection<UavServiceOrder> UavServiceOrders { get; set; } = new List<UavServiceOrder>();
    public ICollection<UavInvoice> UavInvoices { get; set; } = new List<UavInvoice>();
}