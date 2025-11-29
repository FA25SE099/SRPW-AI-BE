using RiceProduction.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetTodayTask;

public class TodayTaskMaterialResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;
    
    /// <summary>
    /// Lượng vật tư dự kiến cần thiết cho tổng diện tích thửa đất.
    /// </summary>
    public decimal PlannedQuantityTotal { get; set; } 
    
    /// <summary>
    /// Chi phí dự kiến cho vật tư này.
    /// </summary>
    public decimal EstimatedAmount { get; set; }
}

/// <summary>
/// DTO cho một Công việc Canh tác (CultivationTask) cần làm hôm nay.
/// </summary>
public class TodayTaskResponse
{
    public Guid CultivationTaskId { get; set; }
    public Guid PlotCultivationId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType TaskType { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TaskPriority Priority { get; set; }

    /// <summary>
    /// Cho biết công việc đã quá hạn (ScheduledEndDate < Today)
    /// </summary>
    public bool IsOverdue { get; set; } // <--- Trường mới

    /// <summary>
    /// Thông tin về thửa đất.
    /// </summary>
    public string PlotSoThuaSoTo { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }

    /// <summary>
    /// Tổng chi phí vật tư dự kiến cho công việc này.
    /// </summary>
    public decimal EstimatedMaterialCost { get; set; }
    
    public List<TodayTaskMaterialResponse> Materials { get; set; } = new();
}