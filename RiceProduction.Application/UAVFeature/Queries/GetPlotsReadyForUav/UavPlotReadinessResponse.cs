using RiceProduction.Domain.Enums;
using System;

namespace RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

public class UavPlotReadinessResponse
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public Guid? PlotCultivationId { get; set; }
    public Guid? CultivationTaskId { get; set; }
    public decimal PlotArea { get; set; }
    
    public DateTime? ReadyDate { get; set; }
    
    public TaskType? TaskType { get; set; }
    
    public decimal EstimatedMaterialCost { get; set; }
    
    public string CultivationTaskName { get; set; } = string.Empty;
  
    public bool IsReady { get; set; }
    public string ReadyStatus { get; set; } = string.Empty;
    public bool HasActiveUavOrder { get; set; }
}