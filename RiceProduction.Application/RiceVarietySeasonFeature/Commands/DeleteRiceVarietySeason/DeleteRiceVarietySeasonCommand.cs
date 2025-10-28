using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.DeleteRiceVarietySeason
{
    public class DeleteRiceVarietySeasonCommand : IRequest<Result<Guid>>
    {
        public Guid RiceVarietySeasonId { get; set; }
    }
}

