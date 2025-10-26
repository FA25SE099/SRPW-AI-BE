using RiceProduction.Domain.Common;

namespace RiceProduction.Domain.Entities;

public class Notification : BaseAuditableEntity
{
    /// <summary>
    /// Recipient user identifier
    /// </summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Type of activity: 'sms_sent', 'comment_on_post', 'sent_friend_request', etc.
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Type of object: 'sms', 'post', 'photo', etc.
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Time when the notification was sent
    /// </summary>
    public DateTime TimeSent { get; set; }

    /// <summary>
    /// Whether the notification has been read
    /// </summary>
    public bool IsUnread { get; set; }

    /// <summary>
    /// Notification content/message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    // Additional SMS-specific fields
    /// <summary>
    /// Phone number for SMS notifications
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// SMS delivery status: 'pending', 'sent', 'delivered', 'failed'
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Message ID from SMS provider
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Error message if SMS failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Retry strategy fields
    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Scheduled time for next retry attempt
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Time of last retry attempt
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// JSON array of retry attempt history
    /// </summary>
    public string? RetryHistory { get; set; }
}
