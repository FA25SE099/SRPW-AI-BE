using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.UpdateRiceVariety
{
    public class UpdateRiceVarietyCommand : IRequest<Result<Guid>>
    {
        public Guid RiceVarietyId { get; set; }
        public string VarietyName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public int? BaseGrowthDurationDays { get; set; }
        public decimal? BaseYieldPerHectare { get; set; }
        public string? Description { get; set; }
        public string? Characteristics { get; set; }
        public bool IsActive { get; set; }
    }
}

