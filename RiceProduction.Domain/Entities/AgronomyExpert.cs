namespace RiceProduction.Domain.Entities;

public class AgronomyExpert : ApplicationUser
{
    public Guid? ClusterId { get; set; }
    public DateTime? AssignedDate { get; set; }
    [ForeignKey("ClusterId")]
    public Cluster? ManagedCluster { get; set; }
    public ICollection<StandardPlan> CreatedStandardPlans { get; set; } = new List<StandardPlan>();
    public ICollection<Alert> ConsultedAlerts { get; set; } = new List<Alert>();
    public ICollection<ProductionPlan> ApprovedProductionPlans { get; set; } = new List<ProductionPlan>();
    public ICollection<YearSeason> ManagedYearSeasons { get; set; } = new List<YearSeason>();
}