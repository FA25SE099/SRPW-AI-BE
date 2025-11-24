using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class WeatherProtocolConfiguration : IEntityTypeConfiguration<WeatherProtocol>
{
    public void Configure(EntityTypeBuilder<WeatherProtocol> builder)
    {
        builder.ToTable("WeatherProtocols");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Source)
            .HasMaxLength(100);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.Name)
            .HasDatabaseName("IX_WeatherProtocols_Name");

        builder.HasIndex(e => e.Source)
            .HasDatabaseName("IX_WeatherProtocols_Source");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_WeatherProtocols_IsActive");

        builder.HasMany(e => e.Thresholds)
            .WithOne(t => t.WeatherProtocol)
            .HasForeignKey(t => t.WeatherProtocolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
