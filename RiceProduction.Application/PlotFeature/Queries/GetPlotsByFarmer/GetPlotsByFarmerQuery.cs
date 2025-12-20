using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByFarmer;

public class GetPlotsByFarmerQuery : IRequest<PagedResult<List<PlotListResponse>>>
{
    public Guid FarmerId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetPlotsByFarmerQueryValidator : AbstractValidator<GetPlotsByFarmerQuery>
{
    public GetPlotsByFarmerQueryValidator()
    {
        RuleFor(x => x.FarmerId)
            .NotEmpty()
            .WithMessage("Farmer ID is required.");

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Current page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
