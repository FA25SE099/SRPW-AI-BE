using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetFarmersBySupervisor;

public class GetFarmersBySupervisorQuery : IRequest<PagedResult<List<FarmerDTO>>>
{
    public Guid SupervisorId { get; set; }
    public bool OnlyAssigned { get; set; } = false; // false = all farmers in cluster, true = only assigned farmers
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}

public class GetFarmersBySupervisorQueryValidator : AbstractValidator<GetFarmersBySupervisorQuery>
{
    public GetFarmersBySupervisorQueryValidator()
    {
        RuleFor(x => x.SupervisorId)
            .NotEmpty()
            .WithMessage("Supervisor ID is required.");

        RuleFor(x => x.CurrentPage)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Current page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
