using RiceProduction.Domain.Common;

namespace RiceProduction.Domain.Entities;
//status: Is Unread: true false
public class Notification : BaseAuditableEntity
{
    public string RecipientId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public DateTime TimeSent { get; set; }
    public bool IsUnread { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Status { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public DateTime? NextRetryAt { get; set; }
    public DateTime? LastRetryAt { get; set; }
    public string? RetryHistory { get; set; }
    public bool Push { get; set; } = false;                   
    public string? PushStatus { get; set; } = "pending";      
    public string? PushError { get; set; }
    public string? DataPayload { get; set; } 
}
