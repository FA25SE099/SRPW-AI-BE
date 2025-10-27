using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class StandardPlanConfiguration : IEntityTypeConfiguration<StandardPlan>
{
    public void Configure(EntityTypeBuilder<StandardPlan> builder)
    {
        builder.ToTable("StandardPlans");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.PlanName)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(e => e.CategoryId)
            .IsRequired();
            
        builder.Property(e => e.TotalDurationDays)
            .IsRequired();
            
        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
        
        builder.HasIndex(e => e.CategoryId)
            .HasDatabaseName("IX_StandardPlans_CategoryId");
            
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_StandardPlans_IsActive");
        
        builder.HasOne(e => e.Category)
            .WithMany(c => c.StandardPlans)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.Creator)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasMany(e => e.StandardPlanStages)
            .WithOne(sps => sps.StandardPlan)
            .HasForeignKey(sps => sps.StandardPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

