using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RiceProduction.Domain.Entities;

public class LateFarmerRecord : BaseAuditableEntity
{
    /// <summary>
    /// The farmer who was late
    /// </summary>
    [Required]
    public Guid FarmerId { get; set; }

    /// <summary>
    /// The cultivation task where the lateness was recorded
    /// </summary>
    [Required]
    public Guid CultivationTaskId { get; set; }

    /// <summary>
    /// The exact time when the lateness was recorded
    /// </summary>
    [Required]
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Optional notes or description about the lateness
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("FarmerId")]
    public Farmer Farmer { get; set; } = null!;

    [ForeignKey("CultivationTaskId")]
    public CultivationTask CultivationTask { get; set; } = null!;
}
