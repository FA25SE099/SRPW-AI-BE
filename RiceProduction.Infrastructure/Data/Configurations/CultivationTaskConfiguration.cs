using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class CultivationTaskConfiguration : IEntityTypeConfiguration<CultivationTask>
{
    public void Configure(EntityTypeBuilder<CultivationTask> builder)
    {
        builder.HasKey(ct => ct.Id);

        // Configure properties
       

        builder.Property(ct => ct.ActualServiceCost)
               .HasPrecision(12, 2)
               .HasDefaultValue(0);

        builder.Property(ct => ct.WeatherConditions)
               .HasMaxLength(200);

        builder.Property(ct => ct.InterruptionReason)
               .HasMaxLength(500);

        // Configure relationships
        builder.HasOne(ct => ct.ProductionPlanTask)
               .WithMany(ppt => ppt.CultivationTasks)
               .HasForeignKey(ct => ct.ProductionPlanTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ct => ct.PlotCultivation)
               .WithMany(pc => pc.CultivationTasks)
               .HasForeignKey(ct => ct.PlotCultivationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ct => ct.AssignedSupervisor)
               .WithMany()
               .HasForeignKey(ct => ct.AssignedToUserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(ct => ct.AssignedVendor)
               .WithMany()
               .HasForeignKey(ct => ct.AssignedToVendorId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(ct => ct.Verifier)
               .WithMany()
               .HasForeignKey(ct => ct.VerifiedBy)
               .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(ct => ct.ProductionPlanTaskId)
               .HasDatabaseName("IX_CultivationTask_ProductionPlanTaskId");

        builder.HasIndex(ct => ct.PlotCultivationId)
               .HasDatabaseName("IX_CultivationTask_PlotCultivationId");


        builder.HasIndex(ct => ct.AssignedToUserId)
               .HasDatabaseName("IX_CultivationTask_AssignedUser");

        builder.HasIndex(ct => ct.AssignedToVendorId)
               .HasDatabaseName("IX_CultivationTask_AssignedVendor");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_CultivationTask_CompletionPercentage", 
                       "[CompletionPercentage] >= 0 AND [CompletionPercentage] <= 100"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_CultivationTask_DateRange", 
                       "[ActualEndDate] IS NULL OR [ActualEndDate] >= [ActualStartDate]"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_CultivationTask_NonNegativeCosts", 
                       "[ActualCost] >= 0 AND [ActualServiceCost] >= 0"));
    }
}