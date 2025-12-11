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
    public Guid TaskId { get; set; }

    /// <summary>
    /// The plot where the lateness occurred
    /// </summary>
    [Required]
    public Guid PlotId { get; set; }

    /// <summary>
    /// The plot cultivation associated with the lateness
    /// </summary>
    [Required]
    public Guid PlotCultivationId { get; set; }

    /// <summary>
    /// The season during which the lateness occurred
    /// </summary>
    [Required]
    public Guid SeasonId { get; set; }

    /// <summary>
    /// The group where the farmer is in late progress
    /// </summary>
    [Required]
    public Guid GroupId { get; set; }

    /// <summary>
    /// The cluster where the lateness was recorded
    /// </summary>
    [Required]
    public Guid ClusterId { get; set; }

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

    [ForeignKey("TaskId")]
    public CultivationTask Task { get; set; } = null!;

    [ForeignKey("PlotId")]
    public Plot Plot { get; set; } = null!;

    [ForeignKey("PlotCultivationId")]
    public PlotCultivation PlotCultivation { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public Season Season { get; set; } = null!;

    [ForeignKey("GroupId")]
    public Group Group { get; set; } = null!;

    [ForeignKey("ClusterId")]
    public Cluster Cluster { get; set; } = null!;
}
