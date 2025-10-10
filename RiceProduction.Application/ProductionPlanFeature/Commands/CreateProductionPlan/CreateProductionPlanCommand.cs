using MediatR;
using RiceProduction.Application.Common.Models.Request;
using FluentValidation;
using System.ComponentModel.DataAnnotations;
using RiceProduction.Application.Common.Models;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;
public class CreateProductionPlanCommand : IRequest<Result<Guid>>
{
    public Guid? GroupId { get; set; }
    public Guid? StandardPlanId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    public DateTime BasePlantingDate { get; set; }

    /// <summary>
    /// Total area of the plan in hectares (ha). This is optional if GroupId is provided, 
    /// otherwise it must be supplied. If GroupId is present, this value is ignored.
    /// </summary>
    public decimal? TotalArea { get; set; }

    /// <summary>
    /// List of all stages defined for this plan, which contain their respective tasks.
    /// </summary>
    public List<ProductionStageRequest> Stages { get; set; } = new();
}

// --- Validators ---

public class ProductionPlanTaskMaterialRequestValidator : AbstractValidator<ProductionPlanTaskMaterialRequest>
{
    public ProductionPlanTaskMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .NotEmpty().WithMessage("Material ID is required.");

        RuleFor(x => x.QuantityPerHa)
            .NotNull().WithMessage("Quantity Per Ha is required.")
            .GreaterThan(0).WithMessage("Quantity Per Ha must be greater than 0.")
            .PrecisionScale(10, 3, true).WithMessage("Quantity Per Ha format is invalid (max 10 digits, 3 decimal places).");
    }
}
public class ProductionPlanTaskRequestValidator : AbstractValidator<ProductionPlanTaskRequest>
{
    public ProductionPlanTaskRequestValidator()
    {
        RuleFor(x => x.TaskName)
            .NotEmpty().WithMessage("Task Name is required.")
            .MaximumLength(255).WithMessage("Task Name cannot exceed 255 characters.");

        RuleFor(x => x.ScheduledDate)
            .NotEmpty().WithMessage("Scheduled Date is required.");

        RuleFor(x => x.ScheduledEndDate)
            .GreaterThanOrEqualTo(x => x.ScheduledDate)
            .WithMessage("Scheduled End Date must be after or equal to the Scheduled Start Date.")
            .When(x => x.ScheduledEndDate.HasValue);

        RuleFor(x => x.SequenceOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sequence Order must be non-negative.");

        RuleForEach(x => x.Materials).SetValidator(new ProductionPlanTaskMaterialRequestValidator());
    }
}

public class ProductionStageRequestValidator : AbstractValidator<ProductionStageRequest>
{
    public ProductionStageRequestValidator()
    {
        RuleFor(x => x.StageName)
            .NotEmpty().WithMessage("Stage Name is required.")
            .MaximumLength(100).WithMessage("Stage Name cannot exceed 100 characters.");
        
        RuleFor(x => x.SequenceOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Stage Sequence Order must be non-negative.");
            
        RuleFor(x => x.Tasks)
            .NotNull().WithMessage("The stage task list cannot be null.")
            .Must(tasks => tasks.Count > 0).WithMessage("Each Stage must contain at least one Task.");

        RuleForEach(x => x.Tasks).SetValidator(new ProductionPlanTaskRequestValidator());
    }
}

public class CreateProductionPlanCommandValidator : AbstractValidator<CreateProductionPlanCommand>
{
    public CreateProductionPlanCommandValidator()
    {
        RuleFor(x => x.PlanName)
            .NotEmpty().WithMessage("Plan Name is required.")
            .MaximumLength(255).WithMessage("Plan Name cannot exceed 255 characters.");

        RuleFor(x => x.BasePlantingDate)
            .NotEmpty().WithMessage("Base Planting Date is required.");

        // NEW LOGIC: TotalArea is required IF GroupId is NULL
        RuleFor(x => x.TotalArea)
            .NotNull().WithMessage("Total Area is required when Group ID is not provided.")
            .GreaterThan(0).WithMessage("Total Area must be greater than 0.")
            .When(x => !x.GroupId.HasValue); 
            
        // Validation for TotalArea if it is provided, regardless of GroupId status
        RuleFor(x => x.TotalArea)
            .GreaterThan(0).WithMessage("Total Area must be greater than 0.")
            .When(x => x.TotalArea.HasValue);

        RuleFor(x => x.Stages)
            .NotNull().WithMessage("The plan must contain a list of production stages.")
            .Must(stages => stages.Count > 0).WithMessage("The plan must contain at least one production stage.");

        RuleForEach(x => x.Stages).SetValidator(new ProductionStageRequestValidator());
    }
}
