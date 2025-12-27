using RiceProduction.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationTaskDetail;
public class TaskMaterialDetailResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;

    // Chi phí dự kiến từ ProductionPlanTask
    public decimal PlannedQuantityPerHa { get; set; }
    public decimal PlannedTotalEstimatedCost { get; set; }

    // Thông tin thực tế từ FarmLog/CultivationTaskMaterial (Nếu có)
    public decimal ActualQuantityUsed { get; set; }
    public decimal ActualCost { get; set; }
    public string? LogNotes { get; set; }
}

public class FarmLogSummaryResponse
{
    public Guid FarmLogId { get; set; }
    public DateTime LoggedDate { get; set; }
    public int CompletionPercentage { get; set; }
    public decimal? ActualAreaCovered { get; set; }
    public string? WorkDescription { get; set; }
    public string[]? PhotoUrls { get; set; }
    public decimal? ActualServiceCost { get; set; }
}

public class CultivationTaskDetailResponse
{
    public Guid CultivationTaskId { get; set; }
    public Guid PlotCultivationId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public bool IsContingency { get; set; }
    
    // Thông tin Version
    public string VersionName { get; set; } = string.Empty;
    public int VersionOrder { get; set; }
    
    // Lịch trình
    public DateTime PlannedScheduledDate { get; set; }
    public DateTime PlannedScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Chi phí
    public decimal EstimatedMaterialCost { get; set; } // Tổng dự kiến
    public decimal ActualMaterialCost { get; set; } // Tổng thực tế
    public decimal ActualServiceCost { get; set; }
    
    // Thông tin thửa đất
    public string PlotName { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }

    public List<TaskMaterialDetailResponse> Materials { get; set; } = new();
    public List<FarmLogSummaryResponse> FarmLogs { get; set; } = new();
}