using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using FluentValidation;
namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;

public class GetAllRiceVarietiesQuery : IRequest<Result<List<RiceVarietyResponse>>>
{
    public string? Search { get; set; }
    
    /// <summary>
    /// Filter by the activity status of the rice variety
    /// </summary>
    public bool? IsActive { get; set; }
    
    /// <summary>
    /// Filter by rice variety category
    /// </summary>
    public Guid? CategoryId { get; set; }
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
