using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialReceipt;

public class ConfirmMaterialReceiptCommand : IRequest<Result<bool>>
{
    public Guid MaterialDistributionId { get; set; }
    public Guid FarmerId { get; set; }
    public string? Notes { get; set; }
}

