using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Commands.UpdateStandardPlan;

public class UpdateStandardPlanCommand : IRequest<Result<Guid>>
{
    public Guid StandardPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalDurationDays { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateStandardPlanCommandValidator : AbstractValidator<UpdateStandardPlanCommand>
{
    public UpdateStandardPlanCommandValidator()
    {
        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("Standard Plan ID is required.");
        
        RuleFor(x => x.PlanName)
            .NotEmpty()
            .WithMessage("Plan name is required.")
            .MaximumLength(255)
            .WithMessage("Plan name cannot exceed 255 characters.");
        
        RuleFor(x => x.TotalDurationDays)
            .GreaterThan(0)
            .WithMessage("Total duration must be greater than 0 days.")
            .LessThanOrEqualTo(365)
            .WithMessage("Total duration cannot exceed 365 days.");
    }
}
