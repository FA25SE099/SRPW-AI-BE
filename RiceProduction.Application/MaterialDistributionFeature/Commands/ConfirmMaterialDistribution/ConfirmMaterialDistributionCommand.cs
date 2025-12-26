using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialDistribution;

public class ConfirmMaterialDistributionCommand : IRequest<Result<bool>>
{
    public Guid MaterialDistributionId { get; set; }
    public Guid SupervisorId { get; set; }
    public DateTime ActualDistributionDate { get; set; }
    public string? Notes { get; set; }
    public List<string>? ImageUrls { get; set; }
}

