using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.Common.Models.Response.ExpertSeasonalEconomics
{
    public class SeasonCostAnalysisResponse
    {
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; } = string.Empty;
        
        public CostOverview Overview { get; set; } = new CostOverview();
        public List<MaterialCostDetail> MaterialCosts { get; set; } = new List<MaterialCostDetail>();
        public List<TaskTypeCostDetail> CostsByTaskType { get; set; } = new List<TaskTypeCostDetail>();
        public List<RiceVarietyCostDetail> CostsByVariety { get; set; } = new List<RiceVarietyCostDetail>();
        public List<MonthlyCostDistribution> MonthlyCosts { get; set; } = new List<MonthlyCostDistribution>();
        public UavServiceCostDetail UavCosts { get; set; } = new UavServiceCostDetail();
    }

    public class CostOverview
    {
        public decimal TotalPlannedCost { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal TotalVariance { get; set; }
        public decimal VariancePercentage { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal ServiceCost { get; set; }
        public decimal UavCost { get; set; }
        public decimal TotalArea { get; set; }
        public decimal CostPerHectare { get; set; }
    }

    public class MaterialCostDetail
    {
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public MaterialType MaterialType { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal QuantityVariance { get; set; }
        public decimal PlannedCost { get; set; }
        public decimal ActualCost { get; set; }
        public decimal CostVariance { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int UsageCount { get; set; }
    }

    public class TaskTypeCostDetail
    {
        public TaskType TaskType { get; set; }
        public int TaskCount { get; set; }
        public decimal PlannedCost { get; set; }
        public decimal ActualCost { get; set; }
        public decimal CostVariance { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal ServiceCost { get; set; }
    }

    public class RiceVarietyCostDetail
    {
        public Guid RiceVarietyId { get; set; }
        public string VarietyName { get; set; } = string.Empty;
        public decimal TotalArea { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostPerHectare { get; set; }
        public int CultivationCount { get; set; }
    }

    public class MonthlyCostDistribution
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal MaterialCost { get; set; }
        public decimal ServiceCost { get; set; }
        public decimal TotalCost { get; set; }
        public int TaskCount { get; set; }
    }

    public class UavServiceCostDetail
    {
        public decimal TotalInvoiced { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalPending { get; set; }
        public int InvoiceCount { get; set; }
        public int PaidInvoiceCount { get; set; }
        public decimal AverageInvoiceAmount { get; set; }
        public decimal TotalAreaServiced { get; set; }
        public decimal AverageRatePerHa { get; set; }
    }
}

