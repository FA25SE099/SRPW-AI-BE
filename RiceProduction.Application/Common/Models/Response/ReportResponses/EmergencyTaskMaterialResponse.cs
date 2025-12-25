namespace RiceProduction.Application.Common.Models.Response.ReportResponses;

/// <summary>
/// Material details for an emergency task
/// </summary>
public class EmergencyTaskMaterialResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ActualQuantity { get; set; }
    public decimal AmountPerMaterial { get; set; }
    public decimal PackagesNeeded { get; set; }
    public decimal PricePerMaterial { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime? PriceValidFrom { get; set; }
    public string? Notes { get; set; }
}
