using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanExecutionSummary;

public class GetPlanExecutionSummaryQuery : IRequest<Result<PlanExecutionSummaryResponse>>
{
    public Guid PlanId { get; set; }
}

public class GetPlanExecutionSummaryQueryValidator : AbstractValidator<GetPlanExecutionSummaryQuery>
{
    public GetPlanExecutionSummaryQueryValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Plan ID is required.");
    }
}

