namespace RiceProduction.Domain.Entities;

public class RiceVarietyCategory : BaseAuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int MinGrowthDays { get; set; }
    
    public int MaxGrowthDays { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string CategoryCode { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public ICollection<RiceVariety> RiceVarieties { get; set; } = new List<RiceVariety>();
    public ICollection<StandardPlan> StandardPlans { get; set; } = new List<StandardPlan>();
}

