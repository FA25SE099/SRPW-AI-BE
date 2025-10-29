namespace RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics
{
    public class HistoricalSeasonComparisonResponse
    {
        public List<SeasonComparisonData> Seasons { get; set; } = new List<SeasonComparisonData>();
        public ComparisonTrends Trends { get; set; } = new ComparisonTrends();
    }

    public class SeasonComparisonData
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        public int Year { get; set; }
        public bool IsActive { get; set; }
        
        public int TotalCultivations { get; set; }
        public decimal TotalArea { get; set; }
        public int TotalFarmers { get; set; }
        
        public decimal TotalCost { get; set; }
        public decimal CostPerHectare { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal ServiceCost { get; set; }
        
        public decimal TotalExpectedYield { get; set; }
        public decimal TotalActualYield { get; set; }
        public decimal YieldPerHectare { get; set; }
        public decimal YieldVariancePercentage { get; set; }
        
        public int CompletedTasks { get; set; }
        public int TotalTasks { get; set; }
        public decimal TaskCompletionRate { get; set; }
        
        public decimal EfficiencyScore { get; set; }
    }

    public class ComparisonTrends
    {
        public TrendAnalysis AreaTrend { get; set; } = new TrendAnalysis();
        public TrendAnalysis CostTrend { get; set; } = new TrendAnalysis();
        public TrendAnalysis YieldTrend { get; set; } = new TrendAnalysis();
        public TrendAnalysis EfficiencyTrend { get; set; } = new TrendAnalysis();
        
        public SeasonComparisonData BestSeason { get; set; } = new SeasonComparisonData();
        public string BestSeasonCriteria { get; set; } = string.Empty;
    }

    public class TrendAnalysis
    {
        public string Metric { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal ChangePercentage { get; set; }
        public decimal AverageValue { get; set; }
        public decimal HighestValue { get; set; }
        public decimal LowestValue { get; set; }
    }
}

