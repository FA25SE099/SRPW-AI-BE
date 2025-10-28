using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetAllRiceVarietySeasons
{
    public class GetAllRiceVarietySeasonsQuery : IRequest<Result<List<RiceVarietySeasonResponse>>>
    {
        public Guid? RiceVarietyId { get; set; }
        public Guid? SeasonId { get; set; }
        public bool? IsRecommended { get; set; }
    }

    public class RiceVarietySeasonResponse
    {
        public Guid Id { get; set; }
        public Guid RiceVarietyId { get; set; }
        public string RiceVarietyName { get; set; } = string.Empty;
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public int GrowthDurationDays { get; set; }
        public decimal? ExpectedYieldPerHectare { get; set; }
        public string OptimalPlantingStart { get; set; } = string.Empty;
        public string? OptimalPlantingEnd { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string? SeasonalNotes { get; set; }
        public bool IsRecommended { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

