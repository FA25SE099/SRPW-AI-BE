using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class ProductionStageConfiguration : IEntityTypeConfiguration<ProductionStage>
{
    public void Configure(EntityTypeBuilder<ProductionStage> builder)
    {
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.StageName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ps => ps.Description)
            .HasMaxLength(500);

        builder.Property(ps => ps.SequenceOrder)
            .IsRequired();

        builder.Property(ps => ps.ColorCode)
            .HasMaxLength(7);

        builder.HasIndex(ps => ps.SequenceOrder);
        builder.HasIndex(ps => ps.StageName);
    }
}

