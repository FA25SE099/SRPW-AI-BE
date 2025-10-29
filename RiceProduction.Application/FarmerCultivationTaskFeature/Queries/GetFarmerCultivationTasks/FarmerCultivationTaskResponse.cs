using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks
{
    public class FarmerCultivationTaskResponse
    {
        public Guid TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskType? TaskType { get; set; }
        public TaskStatus? Status { get; set; }
        public DateTime? ScheduledEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public bool IsContingency { get; set; }
        public string? ContingencyReason { get; set; }
        
        public PlotInfo Plot { get; set; } = new PlotInfo();
        public CultivationInfo Cultivation { get; set; } = new CultivationInfo();
        public List<TaskMaterialInfo> Materials { get; set; } = new List<TaskMaterialInfo>();
        
        public decimal ActualMaterialCost { get; set; }
        public decimal ActualServiceCost { get; set; }
        public decimal TotalCost => ActualMaterialCost + ActualServiceCost;
    }

    public class PlotInfo
    {
        public Guid PlotId { get; set; }
        public int? SoThua { get; set; }
        public int? SoTo { get; set; }
        public decimal Area { get; set; }
        public string? SoilType { get; set; }
    }

    public class CultivationInfo
    {
        public Guid PlotCultivationId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string RiceVarietyName { get; set; } = string.Empty;
        public DateTime PlantingDate { get; set; }
        public CultivationStatus Status { get; set; }
    }

    public class TaskMaterialInfo
    {
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public MaterialType MaterialType { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal PlannedCost { get; set; }
        public decimal? ActualQuantity { get; set; }
        public decimal? ActualCost { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
    }
}

