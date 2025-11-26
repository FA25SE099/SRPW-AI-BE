using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportById;

public class GetReportByIdQuery : IRequest<Result<ReportItemResponse>>
{
    public Guid ReportId { get; set; }
}

