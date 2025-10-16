using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanDetail;

public class GetPlanDetailsForExpertQuery : IRequest<Result<ExpertPlanDetailResponse>>
{
    public Guid PlanId { get; set; }
}

public class GetPlanDetailsForExpertQueryValidator : AbstractValidator<GetPlanDetailsForExpertQuery>
{
    public GetPlanDetailsForExpertQueryValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty().WithMessage("Plan ID is required.");
    }
}