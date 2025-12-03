using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations
{
    public class EmailRequestConfiguration : IEntityTypeConfiguration<EmailRequest>
    {
        public void Configure(EntityTypeBuilder<EmailRequest> builder)
        {
            builder.ToTable("EmailRequests");

            builder.HasKey(e => e.Id);

            // === REQUIRED PROPERTIES ===
            builder.Property(e => e.To)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(e => e.Subject)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(e => e.TextBody)
                   .IsRequired()
                   .HasColumnType("text");

            // === OPTIONAL PROPERTIES ===
            builder.Property(e => e.Cc)
                   .HasMaxLength(500);

            builder.Property(e => e.Bcc)
                   .HasMaxLength(500);

            builder.Property(e => e.HtmlBody)
                   .HasColumnType("text");

            // === STATUS AND TRACKING ===
            builder.Property(e => e.Status)
                   .HasMaxLength(50)
                   .HasDefaultValue("pending");

            builder.Property(e => e.ErrorMessage)
                   .HasMaxLength(1000);

            builder.Property(e => e.MessageId)
                   .HasMaxLength(255);

            // === EMAIL CATEGORIZATION ===
            builder.Property(e => e.EmailType)
                   .HasMaxLength(100)
                   .HasDefaultValue("general");

            builder.Property(e => e.Campaign)
                   .HasMaxLength(255);

            // === RETRY AND PRIORITY ===
            builder.Property(e => e.RetryCount)
                   .HasDefaultValue(0);

            builder.Property(e => e.MaxRetries)
                   .HasDefaultValue(3);

            builder.Property(e => e.Priority)
                   .HasDefaultValue(0);

            // === ADDITIONAL DATA ===
            builder.Property(e => e.DataPayload)
                   .HasColumnType("text");

            // === TRACKING ===
            builder.Property(e => e.IsRead)
                   .HasDefaultValue(false);

            // === INDEXES FOR PERFORMANCE ===
            builder.HasIndex(e => e.To)
                   .HasDatabaseName("IX_EmailRequests_To");

            builder.HasIndex(e => e.Status)
                   .HasDatabaseName("IX_EmailRequests_Status");

            builder.HasIndex(e => e.EmailType)
                   .HasDatabaseName("IX_EmailRequests_EmailType");

            builder.HasIndex(e => e.Campaign)
                   .HasDatabaseName("IX_EmailRequests_Campaign");

            builder.HasIndex(e => e.ScheduledAt)
                   .HasDatabaseName("IX_EmailRequests_ScheduledAt");

            builder.HasIndex(e => e.BatchId)
                   .HasDatabaseName("IX_EmailRequests_BatchId");

            builder.HasIndex(e => e.RecipientId)
                   .HasDatabaseName("IX_EmailRequests_RecipientId");

            builder.HasIndex(e => e.SenderId)
                   .HasDatabaseName("IX_EmailRequests_SenderId");

            // === COMPOSITE INDEXES ===
            builder.HasIndex(e => new { e.Status, e.NextRetryAt })
                   .HasDatabaseName("IX_EmailRequests_Status_NextRetry");

            builder.HasIndex(e => new { e.Status, e.Priority })
                   .HasDatabaseName("IX_EmailRequests_Status_Priority");

            builder.HasIndex(e => new { e.EmailType, e.Status })
                   .HasDatabaseName("IX_EmailRequests_EmailType_Status");

            // === RELATIONSHIPS ===
            builder.HasOne(e => e.Recipient)
                   .WithMany()
                   .HasForeignKey(e => e.RecipientId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.Sender)
                   .WithMany()
                   .HasForeignKey(e => e.SenderId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.Batch)
                   .WithMany(b => b.EmailRequests)
                   .HasForeignKey(e => e.BatchId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

