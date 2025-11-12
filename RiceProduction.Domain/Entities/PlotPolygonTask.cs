using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RiceProduction.Domain.Common;

namespace RiceProduction.Domain.Entities;

/// <summary>
/// Administrative task for supervisors to assign polygon boundaries to plots
/// </summary>
public class PlotPolygonTask : BaseAuditableEntity
{
    [Required]
    public Guid PlotId { get; set; }
    
    [Required]
    public Guid AssignedToSupervisorId { get; set; }
    
    public Guid? AssignedByClusterManagerId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Cancelled
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public int Priority { get; set; } = 1;
    
    // Navigation properties
    [ForeignKey("PlotId")]
    public Plot Plot { get; set; } = null!;
    
    [ForeignKey("AssignedToSupervisorId")]
    public Supervisor AssignedToSupervisor { get; set; } = null!;
    
    [ForeignKey("AssignedByClusterManagerId")]
    public ClusterManager? AssignedByClusterManager { get; set; }
}

