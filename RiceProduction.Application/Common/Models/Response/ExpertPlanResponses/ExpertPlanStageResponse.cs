
namespace RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;

public class ExpertPlanStageResponse
{
    public Guid Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public int? TypicalDurationDays { get; set; }
    public string? ColorCode { get; set; }
    public List<ExpertPlanTaskResponse> Tasks { get; set; } = new();
}