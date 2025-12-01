namespace RiceProduction.Application.Common.Models.Response.PlotMaterialResponses;

public class PlotMaterialItemResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;
    public string? ImgUrl { get; set; }
    public decimal QuantityPerHa { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public DateTime PriceValidFrom { get; set; }
    public DateTime? PriceValidTo { get; set; }
    public bool IsOutdated { get; set; }
    public decimal TotalCost { get; set; }
}

