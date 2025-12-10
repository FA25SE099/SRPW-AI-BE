using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Commands.CreateLateFarmerRecord;

public class CreateLateFarmerRecordCommand : IRequest<Result<Guid>>
{
    public Guid CultivationTaskId { get; set; }
    public string? Notes { get; set; }
}
