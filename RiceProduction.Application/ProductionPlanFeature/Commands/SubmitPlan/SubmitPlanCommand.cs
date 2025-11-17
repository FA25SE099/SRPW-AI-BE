using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.ProductionPlanFeature.Commands.SubmitPlan;

public class SubmitPlanCommand : IRequest<Result<Guid>>
{
    [Required]
    public Guid PlanId { get; set; }

    public Guid? SupervisorId { get; set; }
}

public class SubmitPlanCommandValidator : AbstractValidator<SubmitPlanCommand>
{
    public SubmitPlanCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Plan ID is required.");
    }
}

