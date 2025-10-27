namespace RiceProduction.Application.Common.Models.Response;
public class RiceVarietyResponse
{
    public Guid Id { get; set; }
    public string VarietyName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? BaseGrowthDurationDays { get; set; }
    public decimal? BaseYieldPerHectare { get; set; }
    public string? Description { get; set; }
    public string? Characteristics { get; set; }
    public bool IsActive { get; set; }
}