using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.BulkConfirmMaterialReceipt;

/// <summary>
/// Bulk confirm multiple material receipts at once for a farmer
/// Farmer confirms receipt of all materials that were distributed by supervisor
/// </summary>
public class BulkConfirmMaterialReceiptCommand : IRequest<Result<BulkReceiptConfirmationResponse>>
{
    public Guid FarmerId { get; set; }
    
    /// <summary>
    /// List of distribution IDs to confirm
    /// These should all be distributions where supervisor has confirmed but farmer hasn't
    /// </summary>
    public List<Guid> DistributionIds { get; set; } = new();
    
    public string? Notes { get; set; }
}

public class BulkReceiptConfirmationResponse
{
    public int TotalReceiptsConfirmed { get; set; }
    public List<Guid> ConfirmedDistributionIds { get; set; } = new();
    public int FailedCount { get; set; }
    public List<string> FailedReasons { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

