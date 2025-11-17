using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Security;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.StandardPlanFeature.Commands.CreateStandardPlan;

/// <summary>
/// Command to create a new Standard Plan template by an Agronomy Expert
/// </summary>
[Authorize(Roles = "AgronomyExpert,Administrator")]
public class CreateStandardPlanCommand : IRequest<Result<Guid>>
{
    /// <summary>
    /// Rice variety category (short, medium, long duration)
    /// </summary>
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Total expected duration from planting to harvest (in days)
    /// </summary>
    [Required]
    public int TotalDurationDays { get; set; }

    /// <summary>
    /// Whether this plan is active and available for use
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// All production stages with their tasks and materials
    /// </summary>
    public List<StandardPlanStageRequest> Stages { get; set; } = new();
}

// --- Validators ---

public class StandardPlanTaskMaterialRequestValidator : AbstractValidator<StandardPlanTaskMaterialRequest>
{
    public StandardPlanTaskMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .NotEmpty().WithMessage("Material ID is required.");

        RuleFor(x => x.QuantityPerHa)
            .NotNull().WithMessage("Quantity Per Ha is required.")
            .GreaterThan(0).WithMessage("Quantity Per Ha must be greater than 0.")
            .PrecisionScale(10, 3, true).WithMessage("Quantity Per Ha format is invalid (max 10 digits, 3 decimal places).");
    }
}

public class StandardPlanTaskRequestValidator : AbstractValidator<StandardPlanTaskRequest>
{
    public StandardPlanTaskRequestValidator()
    {
        RuleFor(x => x.TaskName)
            .NotEmpty().WithMessage("Task Name is required.")
            .MaximumLength(255).WithMessage("Task Name cannot exceed 255 characters.");

        RuleFor(x => x.DaysAfter)
            .GreaterThanOrEqualTo(-30).WithMessage("DaysAfter cannot be more than 30 days before planting.")
            .LessThanOrEqualTo(365).WithMessage("DaysAfter cannot exceed 365 days.");

        RuleFor(x => x.DurationDays)
            .GreaterThanOrEqualTo(1).WithMessage("Duration must be at least 1 day.")
            .LessThanOrEqualTo(90).WithMessage("Duration cannot exceed 90 days.");

        RuleFor(x => x.SequenceOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sequence Order must be non-negative.");

        RuleForEach(x => x.Materials)
            .SetValidator(new StandardPlanTaskMaterialRequestValidator());
    }
}

public class StandardPlanStageRequestValidator : AbstractValidator<StandardPlanStageRequest>
{
    public StandardPlanStageRequestValidator()
    {
        RuleFor(x => x.StageName)
            .NotEmpty().WithMessage("Stage Name is required.")
            .MaximumLength(100).WithMessage("Stage Name cannot exceed 100 characters.");

        RuleFor(x => x.SequenceOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Stage Sequence Order must be non-negative.");

        RuleFor(x => x.ExpectedDurationDays)
            .GreaterThanOrEqualTo(1).WithMessage("Expected Duration must be at least 1 day.")
            .When(x => x.ExpectedDurationDays.HasValue);

        RuleFor(x => x.Tasks)
            .NotNull().WithMessage("The stage task list cannot be null.")
            .Must(tasks => tasks.Count > 0).WithMessage("Each Stage must contain at least one Task.");

        RuleForEach(x => x.Tasks)
            .SetValidator(new StandardPlanTaskRequestValidator());
    }
}

public class CreateStandardPlanCommandValidator : AbstractValidator<CreateStandardPlanCommand>
{
    public CreateStandardPlanCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Rice Variety Category is required.");

        RuleFor(x => x.PlanName)
            .NotEmpty().WithMessage("Plan Name is required.")
            .MaximumLength(255).WithMessage("Plan Name cannot exceed 255 characters.");

        RuleFor(x => x.TotalDurationDays)
            .GreaterThan(0).WithMessage("Total Duration must be greater than 0 days.")
            .LessThanOrEqualTo(365).WithMessage("Total Duration cannot exceed 365 days.");

        RuleFor(x => x.Stages)
            .NotNull().WithMessage("The plan must contain a list of production stages.")
            .Must(stages => stages.Count > 0).WithMessage("The plan must contain at least one production stage.");

        RuleForEach(x => x.Stages)
            .SetValidator(new StandardPlanStageRequestValidator());
    }
}

