using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class PestProtocolConfiguration : IEntityTypeConfiguration<PestProtocol>
{
    public void Configure(EntityTypeBuilder<PestProtocol> builder)
    {
        builder.ToTable("PestProtocols");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Type)
            .HasMaxLength(50);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.Name)
            .HasDatabaseName("IX_PestProtocols_Name");

        builder.HasIndex(e => e.Type)
            .HasDatabaseName("IX_PestProtocols_Type");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_PestProtocols_IsActive");

        builder.HasMany(e => e.Thresholds)
            .WithOne(t => t.PestProtocol)
            .HasForeignKey(t => t.PestProtocolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}