using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;

namespace RiceProduction.Application.UavVendorFeature.Queries.GetAllUavVendor;

public class GetAllUavVendorQuery : IRequest<Result<List<UavVendorDto>>>
{
}
