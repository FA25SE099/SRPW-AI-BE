using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportsBySupervisor;

public class GetReportsBySupervisorQuery : IRequest<PagedResult<List<ReportItemResponse>>>
{
    public Guid SupervisorId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ReportType { get; set; }
}

public class GetReportsBySupervisorQueryValidator : AbstractValidator<GetReportsBySupervisorQuery>
{
    public GetReportsBySupervisorQueryValidator()
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
