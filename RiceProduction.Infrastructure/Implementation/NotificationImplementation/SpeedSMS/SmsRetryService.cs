using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Infrastructure.Implementation.NotificationImplementation.SpeedSMS;

public class SmsRetryService : ISmsRetryService
{
    private readonly IUnitOfWork _context;
    private readonly ISmSService _smsService;
    private readonly ILogger<SmsRetryService> _logger;
    private readonly SmsRetryConfiguration _config;

    public SmsRetryService(
        IUnitOfWork context,
        ISmSService smsService,
        ILogger<SmsRetryService> logger,
        IOptions<SmsRetryConfiguration> config)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<SmsResult> SendWithRetryAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _context.Repository<Notification>()
            .FindAsync(n => n.Id == notificationId);

        if (notification == null)
        {
            return new SmsResult 
            { 
                Success = false, 
                ErrorMessage = "Notification not found" 
            };
        }

        return await AttemptSendSms(notification, cancellationToken);
    }

    public async Task<SmsResult> ProcessRetryAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await _context.Repository<Notification>()
            .FindAsync(n => n.Id == notificationId);

        if (notification == null)
        {
            return new SmsResult 
            { 
                Success = false, 
                ErrorMessage = "Notification not found" 
            };
        }

        if (notification.RetryCount >= notification.MaxRetries)
        {
            notification.Status = "failed";
            notification.ErrorMessage = $"Maximum retry attempts ({notification.MaxRetries}) exceeded";
            await _context.CompleteAsync();

            return new SmsResult 
            { 
                Success = false, 
                ErrorMessage = notification.ErrorMessage 
            };
        }

        return await AttemptSendSms(notification, cancellationToken);
    }

    private async Task<SmsResult> AttemptSendSms(
        Domain.Entities.Notification notification, 
        CancellationToken cancellationToken)
    {
        notification.RetryCount++;
        notification.LastRetryAt = DateTime.UtcNow;

        var retryAttempt = new RetryAttempt
        {
            AttemptNumber = notification.RetryCount,
            AttemptTime = DateTime.UtcNow,
            DelayMinutes = notification.RetryCount > 1 ? CalculateDelayMinutes(notification.RetryCount) : 0
        };

        _logger.LogInformation(
            "Attempting SMS send (attempt {RetryCount}/{MaxRetries}) for notification {NotificationId}",
            notification.RetryCount,
            notification.MaxRetries,
            notification.Id);

        try
        {
            // Send SMS through the SMS service
            var smsResultString = await _smsService.SendSMSAsync(
                new[] { notification.PhoneNumber! },
                notification.Content,
                type: 3,
                sender: ""
            );

            // Parse the SMS result
            var isSuccess = !string.IsNullOrEmpty(smsResultString) && 
                           !smsResultString.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                           !smsResultString.Contains("failed", StringComparison.OrdinalIgnoreCase);

            retryAttempt.Status = isSuccess ? "success" : "failed";
            retryAttempt.ErrorMessage = isSuccess ? null : smsResultString;

            // Update retry history
            var history = DeserializeRetryHistory(notification.RetryHistory);
            history.Add(retryAttempt);
            notification.RetryHistory = JsonSerializer.Serialize(history);

            SmsResult result;

            if (isSuccess)
            {
                notification.Status = "sent";
                notification.MessageId = smsResultString;
                notification.NextRetryAt = null;
                notification.ErrorMessage = null;

                _logger.LogInformation(
                    "SMS sent successfully on attempt {RetryCount} for notification {NotificationId}",
                    notification.RetryCount,
                    notification.Id);

                result = new SmsResult
                {
                    Success = true,
                    MessageId = smsResultString,
                    IsTemporaryFailure = false
                };
            }
            else
            {
                // Determine if we should retry
                var shouldRetry = notification.RetryCount < notification.MaxRetries;

                if (shouldRetry)
                {
                    // Schedule next retry
                    var delayMinutes = CalculateDelayMinutes(notification.RetryCount + 1);
                    notification.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                    notification.Status = "pending_retry";
                    notification.ErrorMessage = smsResultString;

                    _logger.LogWarning(
                        "SMS send failed (attempt {RetryCount}), will retry at {NextRetryAt}. Error: {Error}",
                        notification.RetryCount,
                        notification.NextRetryAt,
                        smsResultString);

                    result = new SmsResult
                    {
                        Success = false,
                        ErrorMessage = smsResultString,
                        IsTemporaryFailure = true,
                        RecommendedRetryDelayMinutes = delayMinutes
                    };
                }
                else
                {
                    // Max retries reached
                    notification.Status = "failed";
                    notification.ErrorMessage = $"Failed after {notification.RetryCount} attempts: {smsResultString}";
                    notification.NextRetryAt = null;

                    _logger.LogError(
                        "SMS send permanently failed after {RetryCount} attempts for notification {NotificationId}. Error: {Error}",
                        notification.RetryCount,
                        notification.Id,
                        smsResultString);

                    result = new SmsResult
                    {
                        Success = false,
                        ErrorMessage = notification.ErrorMessage,
                        IsTemporaryFailure = false
                    };
                }
            }

            await _context.CompleteAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending SMS for notification {NotificationId}", notification.Id);

            retryAttempt.Status = "exception";
            retryAttempt.ErrorMessage = ex.Message;

            var history = DeserializeRetryHistory(notification.RetryHistory);
            history.Add(retryAttempt);
            notification.RetryHistory = JsonSerializer.Serialize(history);

            // Determine if we should retry
            var shouldRetry = notification.RetryCount < notification.MaxRetries;

            if (shouldRetry)
            {
                var delayMinutes = CalculateDelayMinutes(notification.RetryCount + 1);
                notification.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                notification.Status = "pending_retry";
                notification.ErrorMessage = ex.Message;
            }
            else
            {
                notification.Status = "failed";
                notification.ErrorMessage = $"Failed after {notification.RetryCount} attempts: {ex.Message}";
                notification.NextRetryAt = null;
            }

            await _context.CompleteAsync();

            return new SmsResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                IsTemporaryFailure = shouldRetry
            };
        }
    }

    private int CalculateDelayMinutes(int retryCount)
    {
        // Exponential backoff: 5, 10, 20, 40, 60 minutes (capped at max)
        var delay = (int)(_config.InitialDelayMinutes * Math.Pow(_config.BackoffMultiplier, retryCount - 1));
        return Math.Min(delay, _config.MaxDelayMinutes);
    }

    private List<RetryAttempt> DeserializeRetryHistory(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<RetryAttempt>();

        try
        {
            return JsonSerializer.Deserialize<List<RetryAttempt>>(json) ?? new List<RetryAttempt>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize retry history");
            return new List<RetryAttempt>();
        }
    }
}
