using MediatR;
using FluentValidation;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationTask;

public class GetFarmLogsByCultivationTaskQuery : IRequest<PagedResult<List<FarmLogDetailResponse>>>
{
    public Guid CultivationTaskId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class GetFarmLogsByCultivationTaskQueryValidator : AbstractValidator<GetFarmLogsByCultivationTaskQuery>
{
    public GetFarmLogsByCultivationTaskQueryValidator()
    {
        RuleFor(x => x.CultivationTaskId)
            .NotEmpty()
            .WithMessage("Cultivation Task ID is required.");

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Current page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100000.");
    }
}
