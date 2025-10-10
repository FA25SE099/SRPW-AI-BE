namespace RiceProduction.Application.Common.Models.Response;
public class ProductionPlanTaskMaterialResponse
{
    public Guid MaterialId { get; set; }
    public string? MaterialName { get; set; } // Added for display
    public string? MaterialUnit { get; set; } // Added for display
    public decimal QuantityPerHa { get; set; }

    /// <summary>
    /// Calculated field: total cost for this material based on TotalArea and UnitPrice.
    /// </summary>
    public decimal EstimatedAmount { get; set; }
}