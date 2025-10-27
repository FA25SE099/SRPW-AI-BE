using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.RecipientId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(n => n.ActivityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.ObjectType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(n => n.Status)
            .HasMaxLength(50);

        builder.Property(n => n.MessageId)
            .HasMaxLength(100);

        builder.Property(n => n.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(n => n.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(n => n.MaxRetries)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(n => n.RetryHistory)
            .HasColumnType("text");

        // Indexes for better query performance
        builder.HasIndex(n => n.RecipientId);
        builder.HasIndex(n => n.ActivityType);
        builder.HasIndex(n => n.IsUnread);
        builder.HasIndex(n => n.TimeSent);
        builder.HasIndex(n => n.Status);
        builder.HasIndex(n => n.NextRetryAt);
    }
}
