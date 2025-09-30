using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class RiceVarietySeasonConfiguration : IEntityTypeConfiguration<RiceVarietySeason>
{
    public void Configure(EntityTypeBuilder<RiceVarietySeason> builder)
    {
        builder.HasKey(rvs => rvs.Id);

        // Composite unique constraint to prevent duplicate variety-season combinations
        builder.HasIndex(rvs => new { rvs.RiceVarietyId, rvs.SeasonId })
               .IsUnique()
               .HasDatabaseName("IX_RiceVarietySeason_Variety_Season");

        // Configure relationships
        builder.HasOne(rvs => rvs.RiceVariety)
               .WithMany(rv => rv.RiceVarietySeasons)
               .HasForeignKey(rvs => rvs.RiceVarietyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rvs => rvs.Season)
               .WithMany(s => s.RiceVarietySeasons)
               .HasForeignKey(rvs => rvs.SeasonId)
               .OnDelete(DeleteBehavior.Cascade);

        // Configure properties
        builder.Property(rvs => rvs.GrowthDurationDays)
               .IsRequired()
               .HasComment("Growth duration in days for this variety in this specific season");

        builder.Property(rvs => rvs.ExpectedYieldPerHectare)
               .HasPrecision(10, 2)
               .HasComment("Expected yield per hectare for this variety-season combination");

        builder.Property(rvs => rvs.RiskLevel)
               .HasConversion<string>()
               .HasMaxLength(10)
               .HasComment("Risk level: Low, Medium, or High");

        builder.Property(rvs => rvs.SeasonalNotes)
               .HasMaxLength(1000)
               .HasComment("Special considerations for this variety-season combination");

        builder.Property(rvs => rvs.IsRecommended)
               .HasDefaultValue(true)
               .HasComment("Whether this variety is recommended for this season");

        // Indexes for performance
        builder.HasIndex(rvs => rvs.RiceVarietyId)
               .HasDatabaseName("IX_RiceVarietySeason_RiceVarietyId");

        builder.HasIndex(rvs => rvs.SeasonId)
               .HasDatabaseName("IX_RiceVarietySeason_SeasonId");

        builder.HasIndex(rvs => rvs.IsRecommended)
               .HasDatabaseName("IX_RiceVarietySeason_IsRecommended");

        builder.HasIndex(rvs => rvs.RiskLevel)
               .HasDatabaseName("IX_RiceVarietySeason_RiskLevel");
    }
}