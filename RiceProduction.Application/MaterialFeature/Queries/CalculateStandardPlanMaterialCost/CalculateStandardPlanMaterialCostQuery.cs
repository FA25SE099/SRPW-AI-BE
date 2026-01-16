using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanMaterialCost;

public class CalculateStandardPlanMaterialCostQuery : IRequest<Result<CalculateMaterialsCostByAreaResponse>>
{
    /// <summary>
    /// Plot ID to retrieve the actual area from. Either PlotId or Area must be provided.
    /// </summary>
    public Guid? PlotId { get; set; }

    /// <summary>
    /// Area in hectares. Either PlotId or Area must be provided.
    /// </summary>
    public decimal? Area { get; set; }

    /// <summary>
    /// Standard Plan ID to get materials from
    /// </summary>
    [Required]
    public Guid StandardPlanId { get; set; }
}

public class CalculateStandardPlanMaterialCostQueryValidator : AbstractValidator<CalculateStandardPlanMaterialCostQuery>
{
    public CalculateStandardPlanMaterialCostQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.PlotId.HasValue || x.Area.HasValue)
            .WithMessage("Either PlotId or Area must be provided.");

        RuleFor(x => x.Area)
            .GreaterThan(0)
            .When(x => x.Area.HasValue)
            .WithMessage("Area must be greater than zero when provided.");

        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("Standard Plan ID is required.");
    }
}
