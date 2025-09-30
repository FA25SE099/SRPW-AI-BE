namespace RiceProduction.Domain.Entities;

public class Farmer : ApplicationUser
{
    /// <summary>
    /// Farm identification number or code
    /// </summary>
    [MaxLength(50)]
    public string? FarmCode { get; set; }

    public ICollection<Plot> OwnedPlots { get; set; } = new List<Plot>();
    public ICollection<FarmLog> FarmLogs { get; set; } = new List<FarmLog>();
    public ICollection<SupervisorFarmerAssignment> FarmerAssignments { get; set; } = new List<SupervisorFarmerAssignment>();
}