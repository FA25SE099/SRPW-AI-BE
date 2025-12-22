using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationPlanById;

public class GetCultivationPlanByIdQuery : IRequest<Result<CultivationPlanDetailResponse>>
{
    public Guid PlanId { get; set; }
}

public class CultivationPlanDetailResponse
{
    public Guid Id { get; set; }
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public DateTime BasePlantingDate { get; set; }
    public decimal TotalArea { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal EstimatedTotalCost { get; set; }
    public string? FarmerName { get; set; }
    public string? ClusterName { get; set; }
    public List<CultivationStageResponse> Stages { get; set; } = new();
}

public class CultivationStageResponse
{
    public Guid Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public int? ExpectedDurationDays { get; set; }
    public List<CultivationTaskResponse> Tasks { get; set; } = new();
}

public class CultivationTaskResponse
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public string TaskStatus { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public List<CultivationTaskMaterialResponse> Materials { get; set; } = new();
}

public class CultivationTaskMaterialResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public string Unit { get; set; } = string.Empty;
}

