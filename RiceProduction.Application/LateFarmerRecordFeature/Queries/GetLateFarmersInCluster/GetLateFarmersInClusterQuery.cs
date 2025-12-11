using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateFarmersInCluster;

public class GetLateFarmersInClusterQuery : IRequest<PagedResult<IEnumerable<FarmerWithLateCountDTO>>>
{
    public Guid? AgronomyExpertId { get; set; }
    public Guid? SupervisorId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
}
