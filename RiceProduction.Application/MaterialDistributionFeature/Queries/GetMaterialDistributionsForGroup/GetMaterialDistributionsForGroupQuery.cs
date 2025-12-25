using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

public class GetMaterialDistributionsForGroupQuery : IRequest<Result<MaterialDistributionsResponse>>
{
    public Guid GroupId { get; set; }
}

public class MaterialDistributionsResponse
{
    public Guid GroupId { get; set; }
    public int TotalDistributions { get; set; }
    public int PendingCount { get; set; }
    public int PartiallyConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }
    public List<MaterialDistributionDetailDto> Distributions { get; set; } = new();
}

public class MaterialDistributionDetailDto
{
    public Guid Id { get; set; }
    public Guid PlotCultivationId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledDistributionDate { get; set; }
    public DateTime DistributionDeadline { get; set; }
    public DateTime? ActualDistributionDate { get; set; }
    public DateTime SupervisorConfirmationDeadline { get; set; }
    public DateTime? FarmerConfirmationDeadline { get; set; }
    public Guid? SupervisorConfirmedBy { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime? SupervisorConfirmedAt { get; set; }
    public string? SupervisorNotes { get; set; }
    public DateTime? FarmerConfirmedAt { get; set; }
    public string? FarmerNotes { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsSupervisorOverdue { get; set; }
    public bool IsFarmerOverdue { get; set; }
}

