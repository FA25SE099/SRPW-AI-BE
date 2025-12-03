using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsAwaitingPolygon;

public class GetPlotsAwaitingPolygonQuery : IRequest<PagedResult<IEnumerable<PlotAwaitingPolygonDto>>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? ClusterId { get; set; }
    public Guid? ClusterManagerId { get; set; }
    public Guid? SupervisorId { get; set; }
    public bool? HasActiveTask { get; set; }
    public string? TaskStatus { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; } = "DaysWaiting";
    public bool Descending { get; set; } = true;
}

public class PlotAwaitingPolygonDto
{
    public Guid PlotId { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal Area { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SoilType { get; set; }
    
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public string? FarmerPhone { get; set; }
    public string? FarmerAddress { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public int DaysAwaitingPolygon { get; set; }
    
    public bool HasActiveTask { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? AssignedToSupervisorId { get; set; }
    public string? AssignedToSupervisorName { get; set; }
    public string? TaskStatus { get; set; }
    public DateTime? TaskAssignedAt { get; set; }
    public int? TaskPriority { get; set; }
    public int? TaskDaysOverdue { get; set; }
}

