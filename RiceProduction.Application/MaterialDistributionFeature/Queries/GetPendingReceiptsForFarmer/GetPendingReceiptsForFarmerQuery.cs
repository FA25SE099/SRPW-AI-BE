using RiceProduction.Application.Common.Models;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetPendingReceiptsForFarmer;

/// <summary>
/// Get all pending material receipts for a farmer (mobile app)
/// Returns distributions where supervisor has confirmed but farmer hasn't
/// </summary>
public class GetPendingReceiptsForFarmerQuery : IRequest<Result<PendingReceiptsForFarmerResponse>>
{
    public Guid FarmerId { get; set; }
}

public class PendingReceiptsForFarmerResponse
{
    public Guid FarmerId { get; set; }
    public int TotalPending { get; set; }
    public int OverdueCount { get; set; }
    public int DueTodayCount { get; set; }
    public int DueTomorrowCount { get; set; }
    public List<MaterialDistributionDetailDto> PendingReceipts { get; set; } = new();
}

