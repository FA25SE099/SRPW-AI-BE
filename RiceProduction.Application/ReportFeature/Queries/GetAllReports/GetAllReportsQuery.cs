using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.ReportFeature.Queries.GetAllReports;

public class GetAllReportsQuery : IRequest<PagedResult<List<ReportItemResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ReportType { get; set; }
}

public class ReportItemResponse
{
    public Guid Id { get; set; }
    public Guid? PlotId { get; set; }
    public string? PlotName { get; set; }
    public decimal? PlotArea { get; set; }
    public Guid? CultivationPlanId { get; set; }
    public string? CultivationPlanName { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string? ReportedByRole { get; set; }
    public DateTime ReportedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string>? Images { get; set; }
    public string? Coordinates { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? FarmerName { get; set; }
    public string? ClusterName { get; set; }
}

