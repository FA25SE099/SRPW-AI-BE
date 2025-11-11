using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics
{
    public class SeasonalEconomicOverviewResponse
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        
        public CultivationSummary Cultivation { get; set; } = new CultivationSummary();
        public CostSummary Costs { get; set; } = new CostSummary();
        public YieldSummary Yields { get; set; } = new YieldSummary();
        public List<RiceVarietyDistribution> VarietyDistribution { get; set; } = new List<RiceVarietyDistribution>();
        public List<CultivationStatusBreakdown> StatusBreakdown { get; set; } = new List<CultivationStatusBreakdown>();
    }

    public class CultivationSummary
    {
        public int TotalPlotCultivations { get; set; }
        public decimal TotalAreaCultivated { get; set; }
        public int TotalGroups { get; set; }
        public int TotalFarmers { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal TaskCompletionRate { get; set; }
    }

    public class CostSummary
    {
        public decimal TotalEstimatedCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal CostVariance { get; set; }
        public decimal ActualMaterialCost { get; set; }
        public decimal ActualServiceCost { get; set; }
        public decimal CostPerHectare { get; set; }
        public decimal UavServiceCost { get; set; }
    }

    public class YieldSummary
    {
        public decimal TotalExpectedYield { get; set; }
        public decimal TotalActualYield { get; set; }
        public decimal YieldVariance { get; set; }
        public decimal AverageYieldPerHectare { get; set; }
        public int HarvestedCultivations { get; set; }
        public int PendingHarvest { get; set; }
    }

    public class RiceVarietyDistribution
    {
        public Guid RiceVarietyId { get; set; }
        public string VarietyName { get; set; } = string.Empty;
        public int CultivationCount { get; set; }
        public decimal TotalArea { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CultivationStatusBreakdown
    {
        public CultivationStatus Status { get; set; }
        public int Count { get; set; }
        public decimal TotalArea { get; set; }
        public decimal Percentage { get; set; }
    }
}

