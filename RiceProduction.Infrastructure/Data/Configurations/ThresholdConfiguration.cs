using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class ThresholdConfiguration : IEntityTypeConfiguration<Threshold>
{
    public void Configure(EntityTypeBuilder<Threshold> builder)
    {
        builder.ToTable("Thresholds");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmergencyProtocolId)
            .IsRequired();

        // === PEST THRESHOLD PROPERTIES ===

        builder.Property(e => e.PestAffectType)
            .HasMaxLength(100);

        builder.Property(e => e.PestSeverityLevel)
            .HasMaxLength(50);

        builder.Property(e => e.PestAreaThresholdPercent);

        builder.Property(e => e.PestPopulationThreshold)
            .HasMaxLength(200);

        builder.Property(e => e.PestDamageThresholdPercent);

        builder.Property(e => e.PestGrowthStage)
            .HasMaxLength(100);

        builder.Property(e => e.PestThresholdNotes)
            .HasMaxLength(500);

        // === WEATHER THRESHOLD PROPERTIES ===

        builder.Property(e => e.WeatherEventType)
            .HasMaxLength(100);

        builder.Property(e => e.WeatherIntensityLevel)
            .HasMaxLength(50);

        builder.Property(e => e.WeatherMeasurementThreshold);

        builder.Property(e => e.WeatherMeasurementUnit)
            .HasMaxLength(50);

        builder.Property(e => e.WeatherThresholdOperator)
            .HasMaxLength(100);

        builder.Property(e => e.WeatherDurationDaysThreshold);

        builder.Property(e => e.WeatherThresholdNotes)
            .HasMaxLength(500);

        // === COMMON PROPERTIES ===

        builder.Property(e => e.ApplicableSeason)
            .HasMaxLength(100);

        builder.Property(e => e.Priority);

        builder.Property(e => e.GeneralNotes)
            .HasMaxLength(1000);

        // === INDEXES ===

        builder.HasIndex(e => e.EmergencyProtocolId)
            .HasDatabaseName("IX_Thresholds_EmergencyProtocolId");

        builder.HasIndex(e => e.PestProtocolId)
            .HasDatabaseName("IX_Thresholds_PestProtocolId");

        builder.HasIndex(e => e.WeatherProtocolId)
            .HasDatabaseName("IX_Thresholds_WeatherProtocolId");

        builder.HasIndex(e => e.RiceVarietyId)
            .HasDatabaseName("IX_Thresholds_RiceVarietyId");

        builder.HasIndex(e => e.ApplicableSeason)
            .HasDatabaseName("IX_Thresholds_ApplicableSeason");

        builder.HasIndex(e => e.Priority)
            .HasDatabaseName("IX_Thresholds_Priority");

        // === RELATIONSHIPS ===

        builder.HasOne(e => e.EmergencyProtocol)
            .WithMany(ep => ep.Thresholds)
            .HasForeignKey(e => e.EmergencyProtocolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.PestProtocol)
            .WithMany(p => p.Thresholds)
            .HasForeignKey(e => e.PestProtocolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.WeatherProtocol)
            .WithMany(w => w.Thresholds)
            .HasForeignKey(e => e.WeatherProtocolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RiceVariety)
            .WithMany()
            .HasForeignKey(e => e.RiceVarietyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
