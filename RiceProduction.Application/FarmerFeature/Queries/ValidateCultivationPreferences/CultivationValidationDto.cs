namespace RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;

public class CultivationValidationDto
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Errors { get; set; } = new();
    public List<ValidationIssue> Warnings { get; set; } = new();
    public List<ValidationRecommendation> Recommendations { get; set; } = new();
    public DateTime? EstimatedHarvestDate { get; set; }
    public int? GrowthDurationDays { get; set; }
    public decimal? ExpectedYield { get; set; }
    public decimal? EstimatedRevenue { get; set; }
}

public class ValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error"; // Error, Warning, Info
}

public class ValidationRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Alternative, Optimization, etc.
}



