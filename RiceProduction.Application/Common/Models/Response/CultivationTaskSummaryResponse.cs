using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response;

public class CultivationTaskSummaryResponse
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType? TaskType { get; set; }
    public Domain.Enums.TaskStatus? Status { get; set; }
    
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    
    public decimal ActualMaterialCost { get; set; }
    public decimal ActualServiceCost { get; set; }
    
    public bool IsContingency { get; set; }
    public string? ContingencyReason { get; set; }
}

