namespace RiceProduction.Application.Common.Models.Response.SupervisorResponses;

public class SupervisorProfileResponse
{
    public Guid SupervisorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    
    // Cluster information
    public Guid? ClusterId { get; set; }
    public string? ClusterName { get; set; }
    
    // Statistics
    public int TotalGroupsSupervised { get; set; }
    public int ActiveGroupsThisSeason { get; set; }
    public int CompletedPolygonTasks { get; set; }
    public int PendingPolygonTasks { get; set; }
    
    // Account info
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
