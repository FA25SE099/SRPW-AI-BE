using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ReportResponses;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportWithEmergencyMaterials;

public class GetReportWithEmergencyMaterialsQuery : IRequest<Result<ReportWithEmergencyMaterialsResponse>>
{
    public Guid ReportId { get; set; }
}
