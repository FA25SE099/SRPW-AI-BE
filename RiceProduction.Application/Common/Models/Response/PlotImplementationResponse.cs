using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response;

public class PlotImplementationResponse
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public string SoThua { get; set; } = string.Empty;
    public string SoTo { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    
    public Guid ProductionPlanId { get; set; }
    public string ProductionPlanName { get; set; } = string.Empty;
    
    public string SeasonName { get; set; } = string.Empty;
    public string RiceVarietyName { get; set; } = string.Empty;
    public DateTime PlantingDate { get; set; }
    
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal CompletionPercentage { get; set; }
    
    public List<PlotTaskDetail> Tasks { get; set; } = new();
}

public class PlotTaskDetail
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType? TaskType { get; set; }
    public Domain.Enums.TaskStatus? Status { get; set; }
    public int ExecutionOrder { get; set; }
    
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    
    public decimal ActualMaterialCost { get; set; }
    public List<TaskMaterialDetail> Materials { get; set; } = new();
}

public class TaskMaterialDetail
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal? ActualCost { get; set; }
    public string Unit { get; set; } = string.Empty;
}

