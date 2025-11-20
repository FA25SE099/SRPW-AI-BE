using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlotImplementation;

public class GetPlotImplementationQuery : IRequest<Result<PlotImplementationResponse>>
{
    public Guid PlotId { get; set; }
    public Guid ProductionPlanId { get; set; }
}

public class GetPlotImplementationQueryValidator : AbstractValidator<GetPlotImplementationQuery>
{
    public GetPlotImplementationQueryValidator()
    {
        RuleFor(x => x.PlotId)
            .NotEmpty().WithMessage("Plot ID is required.");
            
        RuleFor(x => x.ProductionPlanId)
            .NotEmpty().WithMessage("Production Plan ID is required.");
    }
}

