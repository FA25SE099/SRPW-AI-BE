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


        builder.HasIndex(pp => pp.StandardPlanId)
               .HasDatabaseName("IX_ProductionPlan_StandardPlanId");

        builder.HasIndex(pp => new { pp.Status, pp.BasePlantingDate })
               .HasDatabaseName("IX_ProductionPlan_Status_PlantingDate");

        // Check constraints
    }
}