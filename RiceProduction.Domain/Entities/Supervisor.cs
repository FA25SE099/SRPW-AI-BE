namespace RiceProduction.Domain.Entities;

public class Supervisor : ApplicationUser
{
    /// <summary>
    /// Maximum number of farmers this supervisor can manage
    /// </summary>
    public int MaxFarmerCapacity { get; set; } = 10;

    /// <summary>
    /// Current number of assigned farmers
    /// </summary>
    public int CurrentFarmerCount { get; set; } = 0;
    // Navigation properties
    public ICollection<Group> SupervisedGroups { get; set; } = new List<Group>();
    public ICollection<CultivationTask> AssignedTasks { get; set; } = new List<CultivationTask>();
    public ICollection<SupervisorFarmerAssignment> SupervisorAssignments { get; set; } = new List<SupervisorFarmerAssignment>();
}