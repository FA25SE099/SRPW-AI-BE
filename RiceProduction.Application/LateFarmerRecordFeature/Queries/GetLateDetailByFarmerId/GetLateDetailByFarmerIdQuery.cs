using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateDetailByFarmerId;

public class GetLateDetailByFarmerIdQuery : IRequest<Result<FarmerLateDetailDTO>>
{
    public Guid FarmerId { get; set; }
}
