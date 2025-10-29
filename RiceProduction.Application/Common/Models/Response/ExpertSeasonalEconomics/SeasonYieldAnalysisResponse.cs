namespace RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics
{
    public class SeasonYieldAnalysisResponse
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        
        public YieldOverview Overview { get; set; } = new YieldOverview();
        public List<RiceVarietyYieldDetail> YieldByVariety { get; set; } = new List<RiceVarietyYieldDetail>();
        public List<GroupYieldDetail> YieldByGroup { get; set; } = new List<GroupYieldDetail>();
        public List<YieldPerformanceCategory> PerformanceCategories { get; set; } = new List<YieldPerformanceCategory>();
    }

    public class YieldOverview
    {
        public int TotalCultivations { get; set; }
        public int HarvestedCultivations { get; set; }
        public int PendingHarvest { get; set; }
        public decimal TotalArea { get; set; }
        public decimal HarvestedArea { get; set; }
        public decimal TotalExpectedYield { get; set; }
        public decimal TotalActualYield { get; set; }
        public decimal YieldVariance { get; set; }
        public decimal VariancePercentage { get; set; }
        public decimal AverageYieldPerHectare { get; set; }
        public decimal ExpectedYieldPerHectare { get; set; }
        public decimal HighestYieldPerHectare { get; set; }
        public decimal LowestYieldPerHectare { get; set; }
    }

    public class RiceVarietyYieldDetail
    {
        public Guid RiceVarietyId { get; set; }
        public string VarietyName { get; set; } = string.Empty;
        public int CultivationCount { get; set; }
        public int HarvestedCount { get; set; }
        public decimal TotalArea { get; set; }
        public decimal ExpectedYieldPerHa { get; set; }
        public decimal TotalExpectedYield { get; set; }
        public decimal TotalActualYield { get; set; }
        public decimal ActualYieldPerHa { get; set; }
        public decimal YieldVariance { get; set; }
        public decimal VariancePercentage { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostPerTon { get; set; }
    }

    public class GroupYieldDetail
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int PlotCount { get; set; }
        public decimal TotalArea { get; set; }
        public decimal TotalExpectedYield { get; set; }
        public decimal TotalActualYield { get; set; }
        public decimal AverageYieldPerHa { get; set; }
        public decimal YieldVariance { get; set; }
        public int HarvestedCount { get; set; }
    }

    public class YieldPerformanceCategory
    {
        public string Category { get; set; } = string.Empty;
        public decimal MinYieldPerHa { get; set; }
        public decimal MaxYieldPerHa { get; set; }
        public int CultivationCount { get; set; }
        public decimal TotalArea { get; set; }
        public decimal Percentage { get; set; }
    }
}

