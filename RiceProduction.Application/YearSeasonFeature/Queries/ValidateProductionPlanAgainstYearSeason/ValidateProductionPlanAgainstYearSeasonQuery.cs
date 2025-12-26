using MediatR;
using RiceProduction.Application.Common.Models;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.YearSeasonFeature.Queries.ValidateProductionPlanAgainstYearSeason;

public class ValidateProductionPlanAgainstYearSeasonQuery : IRequest<Result<ProductionPlanValidationDto>>
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    public DateTime BasePlantingDate { get; set; }
}

public class ProductionPlanValidationDto
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Errors { get; set; } = new();
    public List<ValidationIssue> Warnings { get; set; } = new();
    public YearSeasonValidationContext? YearSeasonContext { get; set; }
}

public class ValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Error", "Warning", "Info"
}

public class YearSeasonValidationContext
{
    public Guid YearSeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? PlanningWindowStart { get; set; }
    public DateTime? PlanningWindowEnd { get; set; }
    public int? AllowedPlantingFlexibilityDays { get; set; }
    public DateTime? GroupPlantingDate { get; set; }
    public int? DaysUntilPlanningWindowEnd { get; set; }
    public int? DaysUntilSeasonStart { get; set; }
    public bool IsPlanningWindowOpen { get; set; }
}

