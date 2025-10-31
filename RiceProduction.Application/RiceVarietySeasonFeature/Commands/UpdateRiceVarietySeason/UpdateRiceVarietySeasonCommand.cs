using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.UpdateRiceVarietySeason
{
    public class UpdateRiceVarietySeasonCommand : IRequest<Result<Guid>>
    {
        public Guid RiceVarietySeasonId { get; set; }
        public int GrowthDurationDays { get; set; }
        public decimal? ExpectedYieldPerHectare { get; set; }
        public string OptimalPlantingStart { get; set; } = string.Empty;
        public string? OptimalPlantingEnd { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string? SeasonalNotes { get; set; }
        public bool IsRecommended { get; set; }
    }
}

