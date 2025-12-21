using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Commands.StartCultivationTask;

public class StartCultivationTaskCommand : IRequest<Result<StartCultivationTaskResponse>>
{
    /// <summary>
    /// The ID of the cultivation task to start
    /// </summary>
    public Guid CultivationTaskId { get; set; }

    /// <summary>
    /// Optional weather conditions at the start of the task
    /// </summary>
    public string? WeatherConditions { get; set; }

    /// <summary>
    /// Optional notes from the farmer when starting the task
    /// </summary>
    public string? Notes { get; set; }
}

