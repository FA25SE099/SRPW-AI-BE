using MediatR;
using FluentValidation;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationVersionFeature.Queries.GetVersionsByPlotCultivation;

public class GetVersionsByPlotCultivationQuery : IRequest<Result<List<CultivationVersionResponse>>>
{
    public Guid PlotCultivationId { get; set; }
}

public class GetVersionsByPlotCultivationQueryValidator : AbstractValidator<GetVersionsByPlotCultivationQuery>
{
    public GetVersionsByPlotCultivationQueryValidator()
    {
        RuleFor(x => x.PlotCultivationId)
            .NotEmpty()
            .WithMessage("Plot Cultivation ID is required.");
    }
}

public class CultivationVersionResponse
{
    public Guid Id { get; set; }
    public Guid PlotCultivationId { get; set; }
    public string VersionName { get; set; } = string.Empty;
    public int VersionOrder { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TaskCount { get; set; }
}
