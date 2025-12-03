using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Domain.Entities;
//status: Is Active: true false
//một trạng thái khác là được phân công vào farmer hay sup nào
public class SupervisorFarmerAssignment : BaseAuditableEntity
{
    [Required]
    public Guid SupervisorId { get; set; }

    [Required]
    public Guid FarmerId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Assignment notes or reason
    /// </summary>
    public string? AssignmentNotes { get; set; }

    /// <summary>
    /// Assignment priority level
    /// </summary>
    public int Priority { get; set; } = 1;

    // Navigation properties
    public Supervisor Supervisor { get; set; } = null!;
    public Farmer Farmer { get; set; } = null!;
}