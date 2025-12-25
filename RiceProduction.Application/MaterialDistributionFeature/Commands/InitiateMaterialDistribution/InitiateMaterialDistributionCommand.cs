using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialDistributionFeature.Commands.InitiateMaterialDistribution;

public class InitiateMaterialDistributionCommand : IRequest<Result<InitiateMaterialDistributionResponse>>
{
    public Guid GroupId { get; set; }
    public Guid ProductionPlanId { get; set; }
    public List<MaterialDistributionItem> Materials { get; set; } = new();
}

public class MaterialDistributionItem
{
    public Guid PlotCultivationId { get; set; }
    public Guid MaterialId { get; set; }
    public Guid? RelatedTaskId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime ScheduledDate { get; set; }
}

public class InitiateMaterialDistributionResponse
{
    public Guid GroupId { get; set; }
    public int DistributionsCreated { get; set; }
    public List<MaterialDistributionDto> Distributions { get; set; } = new();
}

public class MaterialDistributionDto
{
    public Guid Id { get; set; }
    public Guid PlotCultivationId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public string FarmerName { get; set; } = string.Empty;
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledDistributionDate { get; set; }
    public DateTime DistributionDeadline { get; set; }
    public DateTime SupervisorConfirmationDeadline { get; set; }
}

