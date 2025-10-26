namespace RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

public class StandardPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int TotalDuration { get; set; }
    public bool IsActive { get; set; }
    public int TotalTasks { get; set; }
    public int TotalStages { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
