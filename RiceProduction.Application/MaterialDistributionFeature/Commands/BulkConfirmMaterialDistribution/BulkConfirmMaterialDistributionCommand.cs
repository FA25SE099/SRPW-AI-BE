using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.BulkConfirmMaterialDistribution;

/// <summary>
/// Bulk confirm multiple material distributions at once for a plot cultivation
/// Supervisor provides all materials for the entire plan and confirms with images
/// </summary>
public class BulkConfirmMaterialDistributionCommand : IRequest<Result<BulkConfirmationResponse>>
{
    public Guid PlotCultivationId { get; set; }
    public Guid SupervisorId { get; set; }
    public DateTime ActualDistributionDate { get; set; }
    public string? Notes { get; set; }
    
    /// <summary>
    /// List of images - one per material type distributed
    /// Example: 10 materials = 10 images (one showing each material)
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}

public class BulkConfirmationResponse
{
    public int TotalDistributionsConfirmed { get; set; }
    public List<Guid> ConfirmedDistributionIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

