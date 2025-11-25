using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;

public class CultivationVersion : BaseAuditableEntity
{
    [Required]
    public Guid PlotCultivationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string VersionName { get; set; } = string.Empty;

    /// <summary>
    /// Sequential order of versions (1 = original, 2 = first revision, etc.)
    /// </summary>
    [Required]
    public int VersionOrder { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime? ActivatedAt { get; set; }

    [ForeignKey("PlotCultivationId")]
    public PlotCultivation PlotCultivation { get; set; } = null!;

    public ICollection<CultivationTask> CultivationTasks { get; set; } = new List<CultivationTask>();
}
