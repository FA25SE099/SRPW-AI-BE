using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialFeature.Commands.CreateMaterial
{
    public class CreateMaterialCommand : IRequest<Result<Guid>>
    {
        public string Name { get; set; } = string.Empty;
        public MaterialType Type { get; set; }
        public decimal? AmmountPerMaterial { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal PricePerMaterial { get; set; }
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime PriceValidFrom { get; set; } = DateTime.UtcNow;
    }
}

