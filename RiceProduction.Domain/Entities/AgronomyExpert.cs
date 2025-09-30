namespace RiceProduction.Domain.Entities;

public class AgronomyExpert : ApplicationUser
{
    public ICollection<StandardPlan> CreatedStandardPlans { get; set; } = new List<StandardPlan>();
    public ICollection<Alert> ConsultedAlerts { get; set; } = new List<Alert>();
    public ICollection<ProductionPlan> ApprovedProductionPlans { get; set; } = new List<ProductionPlan>();

}