using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

public record GetAllStandardPlansQuery : IRequest<Result<List<StandardPlanDto>>>, ICacheable
{
    public Guid? CategoryId { get; init; }
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    
    public bool BypassCache { get; init; } = false;
    public string CacheKey => $"StandardPlans:Category:{CategoryId}:Search:{SearchTerm}:Active:{IsActive}";
    public int SlidingExpirationInMinutes => 30;
    public int AbsoluteExpirationInMinutes => 60;
}
