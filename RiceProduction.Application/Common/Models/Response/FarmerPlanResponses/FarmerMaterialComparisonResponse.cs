public class FarmerMaterialComparisonResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;
    public decimal PlannedQuantityPerHa { get; set; }
    public decimal PlannedEstimatedAmount { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal ActualCost { get; set; }
}