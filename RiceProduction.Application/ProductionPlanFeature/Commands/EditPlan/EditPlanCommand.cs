using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;

public class EditPlanCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    /// <summary>
    /// Base Planting Date có thể được thay đổi.
    /// </summary>
    [Required]
    public DateTime BasePlantingDate { get; set; }

    /// <summary>
    /// Toàn bộ cấu trúc Stages, Tasks và Materials mới.
    /// </summary>
    public List<ProductionStageRequest> Stages { get; set; } = new();
}

public class EditPlanCommandValidator : AbstractValidator<EditPlanCommand>
{
    public EditPlanCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("Plan ID is required for editing.");

        RuleFor(x => x.PlanName)
            .NotEmpty().WithMessage("Plan Name is required.")
            .MaximumLength(255).WithMessage("Plan Name cannot exceed 255 characters.");

        RuleFor(x => x.BasePlantingDate)
            .NotEmpty().WithMessage("Base Planting Date is required.");

        RuleFor(x => x.Stages)
            .NotNull().WithMessage("The plan must contain a list of production stages.")
            .Must(stages => stages.Count > 0).WithMessage("The plan must contain at least one production stage.");

        // Giả định ProductionStageRequestValidator đã được định nghĩa trong CreateProductionPlanCommand
        // RuleForEach(x => x.Stages).SetValidator(new ProductionStageRequestValidator()); 
    }
}
