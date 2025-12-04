using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.MaterialFeature.Commands.UpdateMaterial
{
    public class UpdateMaterialCommand : IRequest<Result<Guid>>
    {
        public Guid MaterialId { get; set; }
        public string Name { get; set; } = string.Empty;
        public MaterialType Type { get; set; }
        public decimal? AmmountPerMaterial { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal? PricePerMaterial { get; set; }
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public bool IsActive { get; set; }
        public DateTime? PriceValidFrom { get; set; }
        public List<string>? imgUrls { get; set; }
    }
}

