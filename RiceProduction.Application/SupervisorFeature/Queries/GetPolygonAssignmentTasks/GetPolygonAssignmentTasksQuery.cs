using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetPolygonAssignmentTasks;

public class GetPolygonAssignmentTasksQuery : IRequest<Result<List<PlotPolygonTaskDto>>>
{
    public Guid SupervisorId { get; set; }
    public string? Status { get; set; } // Filter by status: Pending, InProgress, Completed
}

public class PlotPolygonTaskDto
{
    public Guid Id { get; set; }
    public Guid PlotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public int Priority { get; set; }
    
    // Plot info
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal PlotArea { get; set; }
    public string? SoilType { get; set; }
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public string? FarmerPhone { get; set; }
}

