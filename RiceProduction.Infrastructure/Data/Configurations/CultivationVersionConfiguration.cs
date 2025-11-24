using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class CultivationVersionConfiguration : IEntityTypeConfiguration<CultivationVersion>
{
    public void Configure(EntityTypeBuilder<CultivationVersion> builder)
    {
        builder.ToTable("CultivationVersions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.VersionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ProductionPlanId)
            .IsRequired();

        builder.Property(e => e.VersionOrder)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Reason)
            .HasMaxLength(500);

        builder.HasIndex(e => e.ProductionPlanId)
            .HasDatabaseName("IX_CultivationVersions_ProductionPlanId");

        builder.HasIndex(e => new { e.ProductionPlanId, e.VersionOrder })
            .IsUnique()
            .HasDatabaseName("IX_CultivationVersions_ProductionPlanId_VersionOrder");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_CultivationVersions_IsActive");

        builder.HasOne(e => e.ProductionPlan)
            .WithMany(pp => pp.CultivationVersions)
            .HasForeignKey(e => e.ProductionPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.CultivationTasks)
            .WithOne(ct => ct.Version)
            .HasForeignKey(ct => ct.VersionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
