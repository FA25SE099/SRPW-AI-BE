using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApprovedPlan;
public class GetApprovedPlansQuery : IRequest<PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public Guid? SeasonId { get; set; }

    public int? Year { get; set; }
}

public class GetApprovedPlansQueryValidator : AbstractValidator<GetApprovedPlansQuery>
{
    public GetApprovedPlansQueryValidator()
    {
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1).WithMessage("CurrentPage must be 1 or greater.");
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).WithMessage("PageSize must be 1 or greater.");
        
        RuleFor(x => x.Year)
            .GreaterThanOrEqualTo(2000)
            .When(x => x.Year.HasValue)
            .WithMessage("Year must be a valid year (2000 or later).");
    }
}