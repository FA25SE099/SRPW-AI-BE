using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration for GroupPlot join entity (many-to-many relationship between Group and Plot).
/// Business Rule: A plot can belong to multiple groups, but only one group per season.
/// This allows a plot to be in different groups across different seasons.
/// </summary>
public class GroupPlotConfiguration : IEntityTypeConfiguration<GroupPlot>
{
    public void Configure(EntityTypeBuilder<GroupPlot> builder)
    {
        // Unique constraint to prevent duplicate Group-Plot associations
        // Note: This allows a plot to be in multiple groups, but the same plot-group combination can only exist once
        builder.HasIndex(gp => new { gp.GroupId, gp.PlotId }).IsUnique();
        
        // Indexes for performance
        builder.HasIndex(gp => gp.GroupId);
        builder.HasIndex(gp => gp.PlotId);
        
        // Relationships
        builder.HasOne(gp => gp.Group)
            .WithMany(g => g.GroupPlots)
            .HasForeignKey(gp => gp.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(gp => gp.Plot)
            .WithMany(p => p.GroupPlots)
            .HasForeignKey(gp => gp.PlotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

