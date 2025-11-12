using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class PlotPolygonTaskConfiguration : IEntityTypeConfiguration<PlotPolygonTask>
{
    public void Configure(EntityTypeBuilder<PlotPolygonTask> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(t => t.Notes)
            .HasMaxLength(500);

        builder.Property(t => t.Priority)
            .HasDefaultValue(1);

        // Relationships
        builder.HasOne(t => t.Plot)
            .WithMany()
            .HasForeignKey(t => t.PlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedToSupervisor)
            .WithMany()
            .HasForeignKey(t => t.AssignedToSupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedByClusterManager)
            .WithMany()
            .HasForeignKey(t => t.AssignedByClusterManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(t => t.PlotId);
        builder.HasIndex(t => t.AssignedToSupervisorId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAt);
    }
}

