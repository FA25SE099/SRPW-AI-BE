using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Commands.CreateRiceVariety
{
    public class CreateRiceVarietyCommand : IRequest<Result<Guid>>
    {
        public string VarietyName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public int? BaseGrowthDurationDays { get; set; }
        public decimal? BaseYieldPerHectare { get; set; }
        public string? Description { get; set; }
        public string? Characteristics { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

