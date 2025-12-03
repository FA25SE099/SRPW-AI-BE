using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using FluentValidation;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;

public class GetFarmLogsByCultivationQuery : IRequest<PagedResult<List<FarmLogDetailResponse>>>, ICacheable
{
    public Guid PlotCultivationId { get; set; }
    
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    
    public string CacheKey => $"FarmLogs:Cultivation:{PlotCultivationId}:Page{CurrentPage}";
}

public class GetFarmLogsByCultivationQueryValidator : AbstractValidator<GetFarmLogsByCultivationQuery>
{
    public GetFarmLogsByCultivationQueryValidator()
    {
        RuleFor(x => x.PlotCultivationId).NotEmpty().WithMessage("Plot Cultivation ID is required.");
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);
    }
}