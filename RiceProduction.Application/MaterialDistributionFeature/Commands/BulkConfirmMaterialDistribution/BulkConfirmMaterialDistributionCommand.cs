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
    /// Deprecated: Use DistributionImages instead for individual distribution images
    /// List of images applied to all distributions (kept for backward compatibility)
    /// </summary>
    public List<string>? ImageUrls { get; set; }

    /// <summary>
    /// Optional: Individual images for each distribution
    /// Key: MaterialDistributionId, Value: List of image URLs for that specific distribution
    /// If provided, this takes precedence over ImageUrls
    /// </summary>
    public Dictionary<Guid, List<string>>? DistributionImages { get; set; }
}

public class BulkConfirmationResponse
{
    public int TotalDistributionsConfirmed { get; set; }
    public List<Guid> ConfirmedDistributionIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

