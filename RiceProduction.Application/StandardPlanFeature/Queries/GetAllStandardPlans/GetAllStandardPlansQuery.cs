using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

public record GetAllStandardPlansQuery : IRequest<Result<List<StandardPlanDto>>>,ICacheable
{
    public Guid? CategoryId { get; init; }
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    public string CacheKey => "StandardPlans";
}
