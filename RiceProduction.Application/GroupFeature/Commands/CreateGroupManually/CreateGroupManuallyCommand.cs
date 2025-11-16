using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.GroupFeature.Commands.CreateGroupManually;

public class CreateGroupManuallyCommand : IRequest<Result<Guid>>
{
    public Guid ClusterId { get; set; }
    public Guid? SupervisorId { get; set; }
    public Guid RiceVarietyId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public DateTime PlantingDate { get; set; }
    public List<Guid> PlotIds { get; set; } = new();
    public bool IsException { get; set; } = false;
    public string? ExceptionReason { get; set; }
}

