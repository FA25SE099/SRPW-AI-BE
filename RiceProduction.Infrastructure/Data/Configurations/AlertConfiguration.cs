namespace RiceProduction.Infrastructure.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.Property(a => a.Source)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.Status)
            .HasConversion<string>();

        builder.Property(a => a.AlertType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Description)
            .IsRequired();

        builder.Property(a => a.AiConfidence)
            .HasColumnType("decimal(5,2)");

        builder.Property(a => a.AiRawData)
            .HasColumnType("jsonb");

        builder.Property(a => a.RecommendedMaterials)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(a => a.Source);
        builder.HasIndex(a => a.Severity);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.PlotId);
        builder.HasIndex(a => a.GroupId);
        builder.HasIndex(a => a.ClusterId);
        builder.HasIndex(a => a.AlertType);

        // Relationships
        builder.HasOne(a => a.Plot)
            .WithMany(p => p.Alerts)
            .HasForeignKey(a => a.PlotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Group)
            .WithMany(g => g.Alerts)
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Cluster)
            .WithMany(c => c.Alerts)
            .HasForeignKey(a => a.ClusterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Acknowledger)
            .WithMany()
            .HasForeignKey(a => a.AcknowledgedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Resolver)
            .WithMany()
            .HasForeignKey(a => a.ResolvedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.CreatedEmergencyTask)
            .WithMany()
            .HasForeignKey(a => a.CreatedEmergencyTaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}