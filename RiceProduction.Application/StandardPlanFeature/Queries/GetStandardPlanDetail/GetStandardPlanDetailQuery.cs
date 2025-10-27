using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;

public class GetStandardPlanDetailQuery : IRequest<Result<StandardPlanDetailDto>>,ICacheable
{
    public Guid StandardPlanId { get; set; }
    public string CacheKey => $"StandardPlan:{StandardPlanId}";
}

public class GetStandardPlanDetailQueryValidator : AbstractValidator<GetStandardPlanDetailQuery>
{
    public GetStandardPlanDetailQueryValidator()
    {
        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("Standard Plan ID is required.");
    }
}

