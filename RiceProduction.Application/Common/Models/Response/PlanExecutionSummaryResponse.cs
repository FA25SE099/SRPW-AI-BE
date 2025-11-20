namespace RiceProduction.Application.Common.Models.Response;

public class PlanExecutionSummaryResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string ApprovedByExpert { get; set; } = string.Empty;
    
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public decimal TotalArea { get; set; }
    public int PlotCount { get; set; }
    public int FarmerCount { get; set; }
    
    public int TotalTasksCreated { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksInProgress { get; set; }
    public int TasksPending { get; set; }
    public decimal CompletionPercentage { get; set; }
    
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    
    public DateTime? FirstTaskStarted { get; set; }
    public DateTime? LastTaskCompleted { get; set; }
    
    public List<PlotExecutionSummary> PlotSummaries { get; set; } = new();
}

public class PlotExecutionSummary
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty;
    public string FarmerName { get; set; } = string.Empty;
    public decimal PlotArea { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTasks { get; set; }
    public decimal CompletionRate { get; set; }
}

