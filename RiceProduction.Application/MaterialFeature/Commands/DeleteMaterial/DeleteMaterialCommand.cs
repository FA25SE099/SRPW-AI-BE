using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial
{
    public class DeleteMaterialCommand : IRequest<Result<Guid>>
    {
        public Guid MaterialId { get; set; }
    }
}

