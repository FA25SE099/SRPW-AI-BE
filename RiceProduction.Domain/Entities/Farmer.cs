namespace RiceProduction.Domain.Entities;

public class Farmer : ApplicationUser
{
    /// <summary>
    /// ID of the cluster this farmer belongs to
    /// </summary>
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Farm identification number or code
    /// </summary>
    [MaxLength(50)]
    public string? FarmCode { get; set; }
    public int? NumberOfPlots { get; set; } = 2;
    
    /// <summary>
    /// Current status of the farmer (Normal, Warned, NotAllowed, Resigned)
    /// </summary>
    public FarmerStatus Status { get; set; } = FarmerStatus.Normal;
    
    // Navigation properties
    [ForeignKey("ClusterId")]
    public Cluster? Cluster { get; set; }

    public ICollection<Plot> OwnedPlots { get; set; } = new List<Plot>();
    public ICollection<FarmLog> FarmLogs { get; set; } = new List<FarmLog>();
    public ICollection<SupervisorFarmerAssignment> FarmerAssignments { get; set; } = new List<SupervisorFarmerAssignment>();
    public ICollection<LateFarmerRecord> LateFarmerRecords { get; set; } = new List<LateFarmerRecord>();
}