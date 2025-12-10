using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByFarmerId;

public class GetLateCountByFarmerIdQuery : IRequest<Result<FarmerLateCountDTO>>
{
    public Guid FarmerId { get; set; }
}
