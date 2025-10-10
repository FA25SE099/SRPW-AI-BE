namespace RiceProduction.Application.Common.Models.Response;
public class ProductionStageResponse
{
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public string? Description { get; set; }
    public int? TypicalDurationDays { get; set; }
    public string? ColorCode { get; set; }

    public List<ProductionPlanTaskResponse> Tasks { get; set; } = new();
}