using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Commands.StartCultivationTask;

public class StartCultivationTaskResponse
{
    public Guid CultivationTaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public DateTime ActualStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public string? WeatherConditions { get; set; }
    public string Message { get; set; } = string.Empty;
    
    // Plot information
    public Guid PlotId { get; set; }
    public string PlotReference { get; set; } = string.Empty;
    
    // Cultivation information
    public string SeasonName { get; set; } = string.Empty;
    public string RiceVarietyName { get; set; } = string.Empty;
}

