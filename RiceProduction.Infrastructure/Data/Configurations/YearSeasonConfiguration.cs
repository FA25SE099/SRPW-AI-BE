using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class YearSeasonConfiguration : IEntityTypeConfiguration<YearSeason>
{
    public void Configure(EntityTypeBuilder<YearSeason> builder)
    {
        builder.ToTable("YearSeasons");

        builder.HasKey(ys => ys.Id);

        builder.Property(ys => ys.Year)
            .IsRequired();

        builder.Property(ys => ys.StartDate)
            .IsRequired();

        builder.Property(ys => ys.EndDate)
            .IsRequired();

        builder.Property(ys => ys.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(ys => ys.Season)
            .WithMany(s => s.YearSeasons)
            .HasForeignKey(ys => ys.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ys => ys.Cluster)
            .WithMany(c => c.YearSeasons)
            .HasForeignKey(ys => ys.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ys => ys.ManagedByExpert)
            .WithMany(e => e.ManagedYearSeasons)
            .HasForeignKey(ys => ys.ManagedByExpertId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(ys => ys.RiceVariety)
            .WithMany(rv => rv.YearSeasons)
            .HasForeignKey(ys => ys.RiceVarietyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ys => new { ys.ClusterId, ys.SeasonId, ys.Year })
            .IsUnique()
            .HasDatabaseName("IX_YearSeasons_Cluster_Season_Year");

        builder.HasIndex(ys => ys.Status);
        builder.HasIndex(ys => ys.ManagedByExpertId);
    }
}

