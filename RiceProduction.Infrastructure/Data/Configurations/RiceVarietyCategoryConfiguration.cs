using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class RiceVarietyCategoryConfiguration : IEntityTypeConfiguration<RiceVarietyCategory>
{
    public void Configure(EntityTypeBuilder<RiceVarietyCategory> builder)
    {
        builder.ToTable("RiceVarietyCategories");
        
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.CategoryName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(e => e.CategoryCode)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(e => e.Description)
            .HasMaxLength(500);
            
        builder.Property(e => e.MinGrowthDays)
            .IsRequired();
            
        builder.Property(e => e.MaxGrowthDays)
            .IsRequired();
            
        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
            
        builder.HasIndex(e => e.CategoryCode)
            .IsUnique()
            .HasDatabaseName("IX_RiceVarietyCategories_CategoryCode");
            
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_RiceVarietyCategories_IsActive");
        
        builder.HasMany(e => e.RiceVarieties)
            .WithOne(rv => rv.Category)
            .HasForeignKey(rv => rv.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasMany(e => e.StandardPlans)
            .WithOne(sp => sp.Category)
            .HasForeignKey(sp => sp.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

