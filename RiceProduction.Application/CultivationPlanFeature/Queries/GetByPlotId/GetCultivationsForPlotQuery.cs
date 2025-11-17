using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using FluentValidation;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;

public class GetCultivationsForPlotQuery : IRequest<PagedResult<List<PlotCultivationHistoryResponse>>>, ICacheable
{
    public Guid PlotId { get; set; }

    public Guid FarmerId { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    // CacheKey động
    public string CacheKey => $"Plot:{PlotId}:Cultivations:Page{CurrentPage}:Size{PageSize}";
}

public class GetCultivationsForPlotQueryValidator : AbstractValidator<GetCultivationsForPlotQuery>
{
    public GetCultivationsForPlotQueryValidator()
    {
        RuleFor(x => x.PlotId).NotEmpty().WithMessage("Plot ID is required.");
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}