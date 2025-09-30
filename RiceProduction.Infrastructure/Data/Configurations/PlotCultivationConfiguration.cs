using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class PlotCultivationConfiguration : IEntityTypeConfiguration<PlotCultivation>
{
    public void Configure(EntityTypeBuilder<PlotCultivation> builder)
    {
        builder.HasKey(pc => pc.Id);

        // Configure properties
        builder.Property(pc => pc.PlantingDate)
               .IsRequired();

        builder.Property(pc => pc.ActualYield)
               .HasPrecision(10, 2);

        builder.Property(pc => pc.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasDefaultValue(CultivationStatus.Planned);

        // Configure relationships
        builder.HasOne(pc => pc.Plot)
               .WithMany(p => p.PlotCultivations)
               .HasForeignKey(pc => pc.PlotId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.Season)
               .WithMany(s => s.PlotCultivations)
               .HasForeignKey(pc => pc.SeasonId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pc => pc.RiceVariety)
               .WithMany(rv => rv.PlotCultivations)
               .HasForeignKey(pc => pc.RiceVarietyId)
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint to prevent multiple cultivations of same plot in same season
        builder.HasIndex(pc => new { pc.PlotId, pc.SeasonId })
               .IsUnique()
               .HasDatabaseName("IX_PlotCultivation_Plot_Season");

        // Indexes for performance
        builder.HasIndex(pc => pc.PlotId)
               .HasDatabaseName("IX_PlotCultivation_PlotId");

        builder.HasIndex(pc => pc.SeasonId)
               .HasDatabaseName("IX_PlotCultivation_SeasonId");

        builder.HasIndex(pc => pc.RiceVarietyId)
               .HasDatabaseName("IX_PlotCultivation_RiceVarietyId");

        builder.HasIndex(pc => pc.Status)
               .HasDatabaseName("IX_PlotCultivation_Status");

        builder.HasIndex(pc => pc.PlantingDate)
               .HasDatabaseName("IX_PlotCultivation_PlantingDate");

        builder.HasIndex(pc => new { pc.Status, pc.PlantingDate })
               .HasDatabaseName("IX_PlotCultivation_Status_PlantingDate");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_PlotCultivation_ActualYield", 
                       "[ActualYield] IS NULL OR [ActualYield] >= 0"));
    }
}