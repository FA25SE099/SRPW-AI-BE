using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety
{
    public class DeleteRiceVarietyCommand : IRequest<Result<Guid>>
    {
        public Guid RiceVarietyId { get; set; }
    }
}

