using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Infrastructure.Data.Configurations
{
    public class EmailBatchConfiguration : IEntityTypeConfiguration<EmailBatch>
    {
        public void Configure(EntityTypeBuilder<EmailBatch> builder)
        {
            builder.ToTable("EmailBatches");

            builder.HasKey(e => e.Id);

            // === REQUIRED PROPERTIES ===
            builder.Property(e => e.BatchName)
                   .IsRequired()
                   .HasMaxLength(255);

            // === OPTIONAL PROPERTIES ===
            builder.Property(e => e.Description)
                   .HasMaxLength(1000);

            // === STATISTICS ===
            builder.Property(e => e.TotalEmails)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(e => e.SentCount)
                   .HasDefaultValue(0);

            builder.Property(e => e.FailedCount)
                   .HasDefaultValue(0);

            builder.Property(e => e.PendingCount)
                   .HasDefaultValue(0);

            // === STATUS AND TIMING ===
            builder.Property(e => e.Status)
                   .HasMaxLength(50)
                   .HasDefaultValue("pending");

            // === CATEGORIZATION ===
            builder.Property(e => e.Campaign)
                   .HasMaxLength(100);

            builder.Property(e => e.EmailType)
                   .HasMaxLength(100)
                   .HasDefaultValue("general");

            // === CONFIGURATION ===
            builder.Property(e => e.MaxConcurrency)
                   .HasDefaultValue(5);

            builder.Property(e => e.MaxRetries)
                   .HasDefaultValue(3);

            // === ERROR HANDLING ===
            builder.Property(e => e.ErrorMessage)
                   .HasMaxLength(2000);

            // === INDEXES FOR PERFORMANCE ===
            builder.HasIndex(e => e.Status)
                   .HasDatabaseName("IX_EmailBatches_Status");

            builder.HasIndex(e => e.Campaign)
                   .HasDatabaseName("IX_EmailBatches_Campaign");

            builder.HasIndex(e => e.EmailType)
                   .HasDatabaseName("IX_EmailBatches_EmailType");

            builder.HasIndex(e => e.CreatedByUserId)
                   .HasDatabaseName("IX_EmailBatches_CreatedByUserId");

            builder.HasIndex(e => e.CreatedAt)
                   .HasDatabaseName("IX_EmailBatches_CreatedAt");

            // === COMPOSITE INDEXES ===
            builder.HasIndex(e => new { e.Status, e.EmailType })
                   .HasDatabaseName("IX_EmailBatches_Status_EmailType");

            builder.HasIndex(e => new { e.Campaign, e.Status })
                   .HasDatabaseName("IX_EmailBatches_Campaign_Status");

            builder.HasIndex(e => new { e.CreatedAt, e.Status })
                   .HasDatabaseName("IX_EmailBatches_CreatedAt_Status");

            // === RELATIONSHIPS ===
            builder.HasOne(e => e.CreatedByUser)
                   .WithMany()
                   .HasForeignKey(e => e.CreatedByUserId)
                   .OnDelete(DeleteBehavior.SetNull);

            // Navigation property for EmailRequests is already configured in EmailRequestConfiguration
        }
    }
}

