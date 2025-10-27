using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.Common.Interfaces;

/// <summary>
/// Service for handling SMS retry logic
/// </summary>
public interface ISmsRetryService
{
    /// <summary>
    /// Send SMS with automatic retry logic
    /// </summary>
    /// <param name="notificationId">ID of the notification to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the SMS send operation</returns>
    Task<SmsResult> SendWithRetryAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>
    /// Process a single retry attempt
    /// </summary>
    /// <param name="notificationId">ID of the notification to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the retry attempt</returns>
    Task<SmsResult> ProcessRetryAsync(Guid notificationId, CancellationToken cancellationToken);
}
