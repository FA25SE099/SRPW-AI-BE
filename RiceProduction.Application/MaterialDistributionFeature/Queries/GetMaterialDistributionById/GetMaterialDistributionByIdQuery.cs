using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionById;

/// <summary>
/// Get a single material distribution by ID
/// </summary>
public class GetMaterialDistributionByIdQuery : IRequest<Result<MaterialDistributionDetailDto>>
{
    public Guid DistributionId { get; set; }
}

