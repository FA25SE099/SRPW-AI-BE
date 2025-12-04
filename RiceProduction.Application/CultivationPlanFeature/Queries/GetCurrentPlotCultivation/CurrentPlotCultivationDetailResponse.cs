using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;

public class CurrentPlotCultivationDetailResponse
{
    public Guid PlotCultivationId { get; set; }
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public DateTime SeasonStartDate { get; set; }
    public DateTime SeasonEndDate { get; set; }
    
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public string? RiceVarietyDescription { get; set; }
    
    public DateTime PlantingDate { get; set; }
    public decimal? ExpectedYield { get; set; }
    public decimal? ActualYield { get; set; }
    public decimal? CultivationArea { get; set; }
    public CultivationStatus Status { get; set; }
    
    public Guid? ProductionPlanId { get; set; }
    public string? ProductionPlanName { get; set; }
    public string? ProductionPlanDescription { get; set; }
    
    public Guid? ActiveVersionId { get; set; }
    public string? ActiveVersionName { get; set; }
    
    public List<CultivationTaskSummary> Tasks { get; set; } = new List<CultivationTaskSummary>();
    public CultivationProgress Progress { get; set; } = new CultivationProgress();
}

public class CultivationTaskSummary
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public TaskType TaskType { get; set; }
    public Domain.Enums.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public int? OrderIndex { get; set; }
    public string StageName { get; set; } = string.Empty;
    public List<TaskMaterialSummary> Materials { get; set; } = new List<TaskMaterialSummary>();
}

public class TaskMaterialSummary
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class CultivationProgress
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int DaysElapsed { get; set; }
    public int? EstimatedDaysRemaining { get; set; }
}
