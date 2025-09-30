using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class FarmLogMaterialConfiguration : IEntityTypeConfiguration<FarmLogMaterial>
{
    public void Configure(EntityTypeBuilder<FarmLogMaterial> builder)
    {
        builder.HasKey(flm => flm.Id);

        // Configure properties
        builder.Property(flm => flm.ActualQuantityUsed)
               .HasPrecision(10, 2)
               .IsRequired()
               .HasComment("Actual quantity of material used and recorded in this farm log entry");

        builder.Property(flm => flm.ActualCost)
               .HasPrecision(12, 2)
               .IsRequired()
               .HasComment("Actual cost incurred for this material usage");

        builder.Property(flm => flm.Notes)
               .HasMaxLength(500);

        // Configure relationships
        builder.HasOne(flm => flm.FarmLog)
               .WithMany(fl => fl.FarmLogMaterials)
               .HasForeignKey(flm => flm.FarmLogId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(flm => flm.Material)
               .WithMany()
               .HasForeignKey(flm => flm.MaterialId)
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint to prevent duplicate materials per farm log
        builder.HasIndex(flm => new { flm.FarmLogId, flm.MaterialId })
               .IsUnique()
               .HasDatabaseName("IX_FarmLogMaterial_Log_Material");

        // Indexes for performance
        builder.HasIndex(flm => flm.FarmLogId)
               .HasDatabaseName("IX_FarmLogMaterial_LogId");

        builder.HasIndex(flm => flm.MaterialId)
               .HasDatabaseName("IX_FarmLogMaterial_MaterialId");

        // Check constraints for positive quantities and costs
        builder.ToTable(t => t.HasCheckConstraint("CK_FarmLogMaterial_PositiveQuantity", 
                       "[ActualQuantityUsed] > 0"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_FarmLogMaterial_NonNegativeCost", 
                       "[ActualCost] >= 0"));
    }
}