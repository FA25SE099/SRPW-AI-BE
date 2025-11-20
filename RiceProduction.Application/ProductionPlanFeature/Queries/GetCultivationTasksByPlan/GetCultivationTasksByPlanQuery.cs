using FluentValidation;
using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetCultivationTasksByPlan;

public class GetCultivationTasksByPlanQuery : IRequest<Result<List<CultivationTaskSummaryResponse>>>
{
    public Guid ProductionPlanId { get; set; }
    public TaskStatus? StatusFilter { get; set; }
    public Guid? PlotFilter { get; set; }
}

public class GetCultivationTasksByPlanQueryValidator : AbstractValidator<GetCultivationTasksByPlanQuery>
{
    public GetCultivationTasksByPlanQueryValidator()
    {
        RuleFor(x => x.ProductionPlanId)
            .NotEmpty().WithMessage("Production Plan ID is required.");
    }
}

