namespace RiceProduction.Application.Common.Models;

/// <summary>
/// Configuration for SMS retry strategy
/// </summary>
public class SmsRetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in minutes before first retry
    /// </summary>
    public int InitialDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum delay in minutes between retries
    /// </summary>
    public int MaxDelayMinutes { get; set; } = 60;

    /// <summary>
    /// Multiplier for exponential backoff (e.g., 2.0 doubles the delay each time)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Represents a single retry attempt
/// </summary>
public class RetryAttempt
{
    /// <summary>
    /// Attempt number (1, 2, 3, etc.)
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Timestamp when the attempt was made
    /// </summary>
    public DateTime AttemptTime { get; set; }

    /// <summary>
    /// Result status of the attempt
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Error message if attempt failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Delay in minutes before this attempt
    /// </summary>
    public int DelayMinutes { get; set; }
}
