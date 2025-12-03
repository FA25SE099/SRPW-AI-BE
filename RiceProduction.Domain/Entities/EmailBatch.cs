using RiceProduction.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Domain.Entities
{
    public class EmailBatch : BaseAuditableEntity
    {
        [Required]
        [StringLength(255)]
        public string BatchName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        // Statistics
        public int TotalEmails { get; set; }
        public int SentCount { get; set; } = 0;
        public int FailedCount { get; set; } = 0;
        public int PendingCount { get; set; } = 0;

        // Timing
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Status
        [StringLength(50)]
        public string Status { get; set; } = "pending"; // pending, processing, completed, failed

        [StringLength(100)]
        public string? Campaign { get; set; }

        [StringLength(100)]
        public string EmailType { get; set; } = "general";

        // Configuration
        public int MaxConcurrency { get; set; } = 5;
        public int MaxRetries { get; set; } = 3;

        [StringLength(2000)]
        public string? ErrorMessage { get; set; }

        // Creator tracking
        public Guid? CreatedByUserId { get; set; }
        public virtual ApplicationUser? CreatedByUser { get; set; }

        // Relationships
        public virtual ICollection<EmailRequest> EmailRequests { get; set; } = new List<EmailRequest>();

        // Helper properties
        public double ProgressPercentage => TotalEmails > 0 ?
            ((double)(SentCount + FailedCount) / TotalEmails * 100) : 0;

        public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue ?
            CompletedAt.Value - StartedAt.Value : null;
    }
}
