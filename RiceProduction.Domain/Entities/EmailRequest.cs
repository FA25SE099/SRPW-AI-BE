using RiceProduction.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Domain.Entities
{
    public class EmailRequest : BaseAuditableEntity
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string To { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Cc { get; set; }

        [StringLength(500)]
        public string? Bcc { get; set; }

        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string TextBody { get; set; } = string.Empty;

        public string? HtmlBody { get; set; }

        // Status tracking
        [StringLength(50)]
        public string Status { get; set; } = "pending"; // pending, sent, failed, bounced

        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        [StringLength(255)]
        public string? MessageId { get; set; } // Firebase response ID

        // Retry logic
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public DateTime? NextRetryAt { get; set; }

        // Categorization
        [StringLength(100)]
        public string EmailType { get; set; } = "general"; // welcome, password_reset, notification

        [StringLength(255)]
        public string? Campaign { get; set; }

        public int Priority { get; set; } = 0; // 0 = normal, 1 = high, -1 = low
        public DateTime? ScheduledAt { get; set; }

        // Additional data
        public string? DataPayload { get; set; } // JSON format

        // Tracking
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // Relationships
        public Guid? RecipientId { get; set; }
        public virtual ApplicationUser? Recipient { get; set; }

        public Guid? SenderId { get; set; }
        public virtual ApplicationUser? Sender { get; set; }

        // Batch relationship
        public Guid? BatchId { get; set; }
        public virtual EmailBatch? Batch { get; set; }
    }
}
