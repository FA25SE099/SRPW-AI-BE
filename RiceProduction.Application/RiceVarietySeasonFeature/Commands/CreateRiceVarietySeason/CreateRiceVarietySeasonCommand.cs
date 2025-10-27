using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.CreateRiceVarietySeason
{
    public class CreateRiceVarietySeasonCommand : IRequest<Result<Guid>>
    {
        public Guid RiceVarietyId { get; set; }
        public Guid SeasonId { get; set; }
        public int GrowthDurationDays { get; set; }
        public decimal? ExpectedYieldPerHectare { get; set; }
        public string OptimalPlantingStart { get; set; } = string.Empty;
        public string? OptimalPlantingEnd { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
        public string? SeasonalNotes { get; set; }
        public bool IsRecommended { get; set; } = true;
    }
}

