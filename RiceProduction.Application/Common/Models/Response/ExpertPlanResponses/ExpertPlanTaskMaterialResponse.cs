namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
public class ExpertPlanTaskMaterialResponse
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public decimal EstimatedAmount { get; set; } 
}