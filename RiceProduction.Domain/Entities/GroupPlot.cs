namespace RiceProduction.Domain.Entities;

/// <summary>
/// Join table for many-to-many relationship between Group and Plot.
/// Business Rule: A plot can belong to multiple groups, but only one group per season.
/// This allows a plot to be in different groups across different seasons.
/// </summary>
public class GroupPlot : BaseAuditableEntity
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    public Guid PlotId { get; set; }
    
    // Navigation properties
    [ForeignKey("GroupId")]
    public Group Group { get; set; } = null!;
    
    [ForeignKey("PlotId")]
    public Plot Plot { get; set; } = null!;
}

