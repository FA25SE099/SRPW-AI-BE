using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationPlanFeature.Queries;
public class GetFarmerPlanViewQuery : IRequest<Result<FarmerPlanViewResponse>>
{
    /// ID của Thửa đất Canh tác (PlotCultivation) mà Nông dân muốn xem kế hoạch.
    public Guid PlotCultivationId { get; set; }
}

public class GetFarmerPlanViewQueryValidator : AbstractValidator<GetFarmerPlanViewQuery>
{
    public GetFarmerPlanViewQueryValidator()
    {
        RuleFor(x => x.PlotCultivationId)
            .NotEmpty().WithMessage("Plot Cultivation ID is required to view the plan.");
    }
}