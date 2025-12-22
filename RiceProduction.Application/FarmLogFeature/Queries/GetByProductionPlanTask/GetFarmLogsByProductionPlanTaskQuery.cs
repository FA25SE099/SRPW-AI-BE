using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByProductionPlanTask;

public class GetFarmLogsByProductionPlanTaskQuery : IRequest<PagedResult<List<FarmLogDetailResponse>>>
{
    public Guid ProductionPlanTaskId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetFarmLogsByProductionPlanTaskQueryValidator : AbstractValidator<GetFarmLogsByProductionPlanTaskQuery>
{
    public GetFarmLogsByProductionPlanTaskQueryValidator()
    {
        RuleFor(x => x.ProductionPlanTaskId)
            .NotEmpty()
            .WithMessage("Production Plan Task ID is required.");

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Current page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
