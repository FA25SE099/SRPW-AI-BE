using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class CultivationTaskMaterialConfiguration : IEntityTypeConfiguration<CultivationTaskMaterial>
{
    public void Configure(EntityTypeBuilder<CultivationTaskMaterial> builder)
    {
        builder.HasKey(ctm => ctm.Id);

        // Configure properties
        builder.Property(ctm => ctm.ActualQuantity)
               .HasPrecision(10, 2)
               .IsRequired()
               .HasComment("Actual quantity of material used during task execution");

        builder.Property(ctm => ctm.ActualCost)
               .HasPrecision(12, 2)
               .IsRequired()
               .HasComment("Actual cost incurred for this material");

        builder.Property(ctm => ctm.Notes)
               .HasMaxLength(500);

        // Configure relationships
        builder.HasOne(ctm => ctm.CultivationTask)
               .WithMany(ct => ct.CultivationTaskMaterials)
               .HasForeignKey(ctm => ctm.CultivationTaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ctm => ctm.Material)
               .WithMany()
               .HasForeignKey(ctm => ctm.MaterialId)
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint to prevent duplicate materials per cultivation task
        builder.HasIndex(ctm => new { ctm.CultivationTaskId, ctm.MaterialId })
               .IsUnique()
               .HasDatabaseName("IX_CultivationTaskMaterial_Task_Material");

        // Indexes for performance
        builder.HasIndex(ctm => ctm.CultivationTaskId)
               .HasDatabaseName("IX_CultivationTaskMaterial_TaskId");

        builder.HasIndex(ctm => ctm.MaterialId)
               .HasDatabaseName("IX_CultivationTaskMaterial_MaterialId");

        // Check constraints for positive quantities and costs
    }
}