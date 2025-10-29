using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using FluentValidation;
namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;

public class GetAllRiceVarietiesQuery : IRequest<Result<List<RiceVarietyResponse>>>, ICacheable
{
    public string? Search { get; set; }
    
    public bool? IsActive { get; set; }
    
    public Guid? CategoryId { get; set; }
    
    public bool BypassCache { get; init; } = false;
    public string CacheKey => $"RiceVarieties:Search:{Search}:Active:{IsActive}:Category:{CategoryId}";
    public int SlidingExpirationInMinutes => 60;
    public int AbsoluteExpirationInMinutes => 120;
}

public class GetAllRiceVarietiesQueryValidator : AbstractValidator<GetAllRiceVarietiesQuery>
{
    public GetAllRiceVarietiesQueryValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("Search term cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Search));
    }
}
