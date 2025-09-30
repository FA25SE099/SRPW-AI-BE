using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class ProductionPlanTaskMaterialConfiguration : IEntityTypeConfiguration<ProductionPlanTaskMaterial>
{
    public void Configure(EntityTypeBuilder<ProductionPlanTaskMaterial> builder)
    {
        builder.HasKey(pptm => pptm.Id);

        
        builder.HasOne(pptm => pptm.ProductionPlanTask)
               .WithMany(ppt => ppt.ProductionPlanTaskMaterials)
               .HasForeignKey(pptm => pptm.ProductionPlanTaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pptm => pptm.Material)
               .WithMany()
               .HasForeignKey(pptm => pptm.MaterialId)
               .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint to prevent duplicate materials per task
        builder.HasIndex(pptm => new { pptm.ProductionPlanTaskId, pptm.MaterialId })
               .IsUnique()
               .HasDatabaseName("IX_ProductionPlanTaskMaterial_Task_Material");

        // Indexes for performance
        builder.HasIndex(pptm => pptm.ProductionPlanTaskId)
               .HasDatabaseName("IX_ProductionPlanTaskMaterial_TaskId");

        builder.HasIndex(pptm => pptm.MaterialId)
               .HasDatabaseName("IX_ProductionPlanTaskMaterial_MaterialId");

        // Check constraint for positive quantities and costs
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductionPlanTaskMaterial_PositiveQuantity", 
                       "[EstimatedQuantity] > 0"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductionPlanTaskMaterial_NonNegativeCost", 
                       "[EstimatedCost] >= 0"));
    }
}