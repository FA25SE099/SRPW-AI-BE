namespace RiceProduction.Application.Common.Models;

/// <summary>
/// Result of SMS send operation
/// </summary>
public class SmsResult
{
    /// <summary>
    /// Whether the SMS was sent successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message ID from SMS provider
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Error message if send failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the failure is temporary and should be retried
    /// </summary>
    public bool IsTemporaryFailure { get; set; }

    /// <summary>
    /// Recommended delay in minutes before retrying
    /// </summary>
    public int? RecommendedRetryDelayMinutes { get; set; }
}
