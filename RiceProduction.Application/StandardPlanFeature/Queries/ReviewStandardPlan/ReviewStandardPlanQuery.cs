using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.ReviewStandardPlan;
public class ReviewStandardPlanQuery : IRequest<Result<StandardPlanReviewDto>>, ICacheable
{
    public Guid StandardPlanId { get; set; }
    
    public DateTime SowDate { get; set; }
    
    public decimal AreaInHectares { get; set; }
    
    public bool BypassCache { get; init; } = false;
    public string CacheKey => $"ReviewStandardPlan:{StandardPlanId}:Date:{SowDate:yyyyMMdd}:Area:{AreaInHectares}";
    public int SlidingExpirationInMinutes => 15;
    public int AbsoluteExpirationInMinutes => 30;
}

public class ReviewStandardPlanQueryValidator : AbstractValidator<ReviewStandardPlanQuery>
{
    public ReviewStandardPlanQueryValidator()
    {
        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("Standard Plan ID is required.");
        
        RuleFor(x => x.SowDate)
            .NotEmpty()
            .WithMessage("Sow date is required.")
            .GreaterThanOrEqualTo(DateTime.Today.AddDays(-30))
            .WithMessage("Sow date cannot be more than 30 days in the past.");
        
        RuleFor(x => x.AreaInHectares)
            .GreaterThan(0)
            .WithMessage("Area must be greater than 0 hectares.")
            .LessThanOrEqualTo(10000)
            .WithMessage("Area cannot exceed 10,000 hectares.");
    }
}

