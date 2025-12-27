using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetPendingDistributionsForSupervisor;

/// <summary>
/// Get all pending material distributions for a supervisor
/// Used for supervisor dashboard and notifications
/// </summary>
public class GetPendingDistributionsForSupervisorQuery : IRequest<Result<PendingDistributionsForSupervisorResponse>>
{
    public Guid SupervisorId { get; set; }
}

public class PendingDistributionsForSupervisorResponse
{
    public Guid SupervisorId { get; set; }
    public int TotalPending { get; set; }
    public int OverdueCount { get; set; }
    public int DueTodayCount { get; set; }
    public int DueTomorrowCount { get; set; }
    public List<MaterialDistributionDetailDto> PendingDistributions { get; set; } = new();
}

