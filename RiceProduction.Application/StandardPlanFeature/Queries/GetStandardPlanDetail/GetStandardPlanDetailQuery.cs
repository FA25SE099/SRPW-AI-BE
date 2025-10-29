using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;

public class GetStandardPlanDetailQuery : IRequest<Result<StandardPlanDetailDto>>, ICacheable
{
    public Guid StandardPlanId { get; set; }
    
    public bool BypassCache { get; init; } = false;
    public string CacheKey => $"StandardPlanDetail:{StandardPlanId}";
    public int SlidingExpirationInMinutes => 60;
    public int AbsoluteExpirationInMinutes => 120;
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

