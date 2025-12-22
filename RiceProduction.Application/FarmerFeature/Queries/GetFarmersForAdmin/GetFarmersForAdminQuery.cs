using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.FarmerResponses;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmersForAdmin;

public class GetFarmersForAdminQuery : IRequest<PagedResult<List<FarmerListResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? ClusterId { get; set; }
    public string? FarmerStatus { get; set; }
}

public class GetFarmersForAdminQueryValidator : AbstractValidator<GetFarmersForAdminQuery>
{
    public GetFarmersForAdminQueryValidator()
    {
        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Current page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
