using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class ProductionPlanTaskConfiguration : IEntityTypeConfiguration<ProductionPlanTask>
{
    public void Configure(EntityTypeBuilder<ProductionPlanTask> builder)
    {
        builder.HasKey(ppt => ppt.Id);

        // Configure properties
        builder.Property(ppt => ppt.TaskName)
               .IsRequired()
               .HasMaxLength(255);

        builder.Property(ppt => ppt.Description)
               .HasMaxLength(1000);

        builder.Property(ppt => ppt.TaskType)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(ppt => ppt.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasDefaultValue(TaskStatus.Draft);

        builder.Property(ppt => ppt.Priority)
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasDefaultValue(TaskPriority.Normal);

        builder.Property(ppt => ppt.ScheduledDate)
               .IsRequired();

        builder.Property(ppt => ppt.EstimatedMaterialCost)
               .HasPrecision(12, 2)
               .HasDefaultValue(0);

        builder.Property(ppt => ppt.EstimatedServiceCost)
               .HasPrecision(12, 2)
               .HasDefaultValue(0);

        // Configure relationships
        builder.HasOne(ppt => ppt.ProductionPlan)
               .WithMany(pp => pp.ProductionPlanTasks)
               .HasForeignKey(ppt => ppt.ProductionPlanId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ppt => ppt.StandardPlanTask)
               .WithMany(spt => spt.ProductionPlanTasks)
               .HasForeignKey(ppt => ppt.StandardPlanTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(ppt => ppt.ProductionPlanId)
               .HasDatabaseName("IX_ProductionPlanTask_ProductionPlanId");

        builder.HasIndex(ppt => ppt.StandardPlanTaskId)
               .HasDatabaseName("IX_ProductionPlanTask_StandardPlanTaskId");

        builder.HasIndex(ppt => ppt.ScheduledDate)
               .HasDatabaseName("IX_ProductionPlanTask_ScheduledDate");

        builder.HasIndex(ppt => ppt.Status)
               .HasDatabaseName("IX_ProductionPlanTask_Status");

        builder.HasIndex(ppt => new { ppt.ProductionPlanId, ppt.SequenceOrder })
               .HasDatabaseName("IX_ProductionPlanTask_Plan_Sequence");

        // Check constraint for dates
    }
}