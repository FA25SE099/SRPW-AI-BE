using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class ProductionPlanConfiguration : IEntityTypeConfiguration<ProductionPlan>
{
    public void Configure(EntityTypeBuilder<ProductionPlan> builder)
    {
        builder.HasKey(pp => pp.Id);

        // Configure properties
        builder.Property(pp => pp.PlanName)
               .IsRequired()
               .HasMaxLength(255);

        builder.Property(pp => pp.BasePlantingDate)
               .IsRequired();

        builder.Property(pp => pp.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .HasDefaultValue(TaskStatus.Draft);

        builder.Property(pp => pp.TotalArea)
               .HasPrecision(10, 2);

        // Configure relationships
        builder.HasOne(pp => pp.Group)
               .WithMany(g => g.ProductionPlans)
               .HasForeignKey(pp => pp.GroupId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(pp => pp.PlotCultivation)
               .WithMany(pc => pc.ProductionPlans)
               .HasForeignKey(pp => pp.PlotCultivationId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(pp => pp.StandardPlan)
               .WithMany(sp => sp.ProductionPlans)
               .HasForeignKey(pp => pp.StandardPlanId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(pp => pp.Approver)
               .WithMany()
               .HasForeignKey(pp => pp.ApprovedBy)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(pp => pp.Submitter)
               .WithMany()
               .HasForeignKey(pp => pp.SubmittedBy)
               .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(pp => pp.PlanName)
               .HasDatabaseName("IX_ProductionPlan_PlanName");

        builder.HasIndex(pp => pp.Status)
               .HasDatabaseName("IX_ProductionPlan_Status");

        builder.HasIndex(pp => pp.BasePlantingDate)
               .HasDatabaseName("IX_ProductionPlan_BasePlantingDate");

        builder.HasIndex(pp => pp.GroupId)
               .HasDatabaseName("IX_ProductionPlan_GroupId");

        builder.HasIndex(pp => pp.PlotCultivationId)
               .HasDatabaseName("IX_ProductionPlan_PlotCultivationId");

        builder.HasIndex(pp => pp.StandardPlanId)
               .HasDatabaseName("IX_ProductionPlan_StandardPlanId");

        builder.HasIndex(pp => new { pp.Status, pp.BasePlantingDate })
               .HasDatabaseName("IX_ProductionPlan_Status_PlantingDate");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductionPlan_TotalArea", 
                       "[TotalArea] IS NULL OR [TotalArea] > 0"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductionPlan_ApprovalFlow", 
                       "([ApprovedAt] IS NULL AND [ApprovedBy] IS NULL) OR ([ApprovedAt] IS NOT NULL AND [ApprovedBy] IS NOT NULL)"));
        
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductionPlan_SubmissionFlow", 
                       "([SubmittedAt] IS NULL AND [SubmittedBy] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedBy] IS NOT NULL)"));
    }
}