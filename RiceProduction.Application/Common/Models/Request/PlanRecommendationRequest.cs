using System.Collections.Generic;

namespace RiceProduction.Application.Common.Models.Request;

public class PlanRecommendationRequest
{
    public string ReportContent { get; set; } = string.Empty;
    public string? ExistingPlanContent { get; set; }
    public string Language { get; set; } = "vi";
}

public class PlanRecommendationResponse
{
    public bool Success { get; set; }
    public List<TaskSuggestion> Suggestions { get; set; } = new();
    public string? RawAnalysis { get; set; }
}

public class TaskSuggestion
{
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; // Tại sao đề xuất task này?
}
