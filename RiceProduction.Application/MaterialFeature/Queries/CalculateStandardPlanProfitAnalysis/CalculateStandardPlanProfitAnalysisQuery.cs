using RiceProduction.Application.Common.Models;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanProfitAnalysis;

public class CalculateStandardPlanProfitAnalysisQuery : IRequest<Result<StandardPlanProfitAnalysisResponse>>
{
    /// <summary>
    /// Plot ID to retrieve the actual area from. Either PlotId or Area must be provided.
    /// </summary>
    public Guid? PlotId { get; set; }

    /// <summary>
    /// Area in hectares. Either PlotId or Area must be provided.
    /// </summary>
    public decimal? Area { get; set; }

    /// <summary>
    /// Standard Plan ID to get materials from
    /// </summary>
    [Required]
    public Guid StandardPlanId { get; set; }

    /// <summary>
    /// Expected price for 1kg of rice production (in VND)
    /// </summary>
    [Required]
    public decimal PricePerKgRice { get; set; }

    /// <summary>
    /// Expected amount of rice production per hectare (in kg)
    /// </summary>
    [Required]
    public decimal ExpectedYieldPerHa { get; set; }

    /// <summary>
    /// Other service costs per hectare (labor, machinery, etc.) (in VND)
    /// </summary>
    public decimal OtherServiceCostPerHa { get; set; } = 0M;
}

public class StandardPlanProfitAnalysisResponse
{
    /// <summary>
    /// Area used for calculation (in hectares)
    /// </summary>
    public decimal Area { get; set; }

    /// <summary>
    /// Price for 1kg of rice (in VND)
    /// </summary>
    public decimal PricePerKgRice { get; set; }

    /// <summary>
    /// Expected yield per hectare (in kg)
    /// </summary>
    public decimal ExpectedYieldPerHa { get; set; }

    /// <summary>
    /// Expected revenue per hectare (PricePerKgRice * ExpectedYieldPerHa)
    /// </summary>
    public decimal ExpectedRevenuePerHa { get; set; }

    /// <summary>
    /// Material cost per hectare
    /// </summary>
    public decimal MaterialCostPerHa { get; set; }

    /// <summary>
    /// Other service cost per hectare
    /// </summary>
    public decimal OtherServiceCostPerHa { get; set; }

    /// <summary>
    /// Total cost per hectare (MaterialCostPerHa + OtherServiceCostPerHa)
    /// </summary>
    public decimal TotalCostPerHa { get; set; }

    /// <summary>
    /// Profit per hectare (ExpectedRevenuePerHa - TotalCostPerHa)
    /// </summary>
    public decimal ProfitPerHa { get; set; }

    /// <summary>
    /// Profit margin per hectare (ProfitPerHa / ExpectedRevenuePerHa * 100)
    /// </summary>
    public decimal ProfitMarginPerHa { get; set; }

    /// <summary>
    /// Expected total revenue for the given area
    /// </summary>
    public decimal ExpectedRevenueForArea { get; set; }

    /// <summary>
    /// Total material cost for the given area
    /// </summary>
    public decimal MaterialCostForArea { get; set; }

    /// <summary>
    /// Total other service cost for the given area (OtherServiceCostPerHa * Area)
    /// </summary>
    public decimal OtherServiceCostForArea { get; set; }

    /// <summary>
    /// Total cost for the given area (MaterialCostForArea + OtherServiceCostForArea)
    /// </summary>
    public decimal TotalCostForArea { get; set; }

    /// <summary>
    /// Total profit for the given area (ExpectedRevenueForArea - TotalCostForArea)
    /// </summary>
    public decimal ProfitForArea { get; set; }

    /// <summary>
    /// Profit margin for the given area (ProfitForArea / ExpectedRevenueForArea * 100)
    /// </summary>
    public decimal ProfitMarginForArea { get; set; }

    /// <summary>
    /// Detailed material cost breakdown
    /// </summary>
    public List<MaterialCostSummary> MaterialCostDetails { get; set; } = new List<MaterialCostSummary>();

    /// <summary>
    /// Warnings about missing or invalid prices
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();
}

public class MaterialCostSummary
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public decimal TotalQuantityForArea { get; set; }
    public decimal PackagesNeeded { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostPerHa { get; set; }
}

public class CalculateStandardPlanProfitAnalysisQueryValidator : AbstractValidator<CalculateStandardPlanProfitAnalysisQuery>
{
    public CalculateStandardPlanProfitAnalysisQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.PlotId.HasValue || x.Area.HasValue)
            .WithMessage("Either PlotId or Area must be provided.");

        RuleFor(x => x.Area)
            .GreaterThan(0)
            .When(x => x.Area.HasValue)
            .WithMessage("Area must be greater than zero when provided.");

        RuleFor(x => x.StandardPlanId)
            .NotEmpty()
            .WithMessage("Standard Plan ID is required.");

        RuleFor(x => x.PricePerKgRice)
            .GreaterThan(0)
            .WithMessage("Price per kg of rice must be greater than zero.");

        RuleFor(x => x.ExpectedYieldPerHa)
            .GreaterThan(0)
            .WithMessage("Expected yield per hectare must be greater than zero.");

        RuleFor(x => x.OtherServiceCostPerHa)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Other service cost per hectare must be greater than or equal to zero.");
    }
}
