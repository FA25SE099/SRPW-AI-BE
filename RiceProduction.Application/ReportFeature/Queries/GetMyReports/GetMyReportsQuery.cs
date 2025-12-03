using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;

namespace RiceProduction.Application.ReportFeature.Queries.GetMyReports;

public class GetMyReportsQuery : IRequest<PagedResult<List<ReportItemResponse>>>
{
    public Guid UserId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ReportType { get; set; }
}



