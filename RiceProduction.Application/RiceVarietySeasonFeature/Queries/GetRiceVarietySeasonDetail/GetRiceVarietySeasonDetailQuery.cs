using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetRiceVarietySeasonDetail
{
    public class GetRiceVarietySeasonDetailQuery : IRequest<Result<RiceVarietySeasonDetailResponse>>
    {
        public Guid RiceVarietySeasonId { get; set; }
    }

    public class RiceVarietySeasonDetailResponse
    {
        public Guid Id { get; set; }
        public Guid RiceVarietyId { get; set; }
        public string RiceVarietyName { get; set; } = string.Empty;
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string SeasonStartDate { get; set; } = string.Empty;
        public string SeasonEndDate { get; set; } = string.Empty;
        public int GrowthDurationDays { get; set; }
        public decimal? ExpectedYieldPerHectare { get; set; }
        public string OptimalPlantingStart { get; set; } = string.Empty;
        public string? OptimalPlantingEnd { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string? SeasonalNotes { get; set; }
        public bool IsRecommended { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}

