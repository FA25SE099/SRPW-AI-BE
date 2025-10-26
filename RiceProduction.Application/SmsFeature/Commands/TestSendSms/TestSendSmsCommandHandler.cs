using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SmsFeature.Commands.TestSendSms;

public class TestSendSmsCommandHandler : IRequestHandler<TestSendSmsCommand, TestSendSmsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ISmsRetryService _smsRetryService;
    private readonly ILogger<TestSendSmsCommandHandler> _logger;

    public TestSendSmsCommandHandler(
        IApplicationDbContext context,
        ISmsRetryService smsRetryService,
        ILogger<TestSendSmsCommandHandler> logger)
    {
        _context = context;
        _smsRetryService = smsRetryService;
        _logger = logger;
    }

    public async Task<TestSendSmsResponse> Handle(TestSendSmsCommand request, CancellationToken cancellationToken)
    {
        // Create notification record with pending status
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = request.RecipientId,
            ActivityType = "sms_sent",
            ObjectType = "sms",
            TimeSent = DateTime.UtcNow,
            IsUnread = true,
            Content = request.Message,
            PhoneNumber = request.PhoneNumber,
            Status = "pending",
            MaxRetries = 3
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Attempting to send SMS to {PhoneNumber} with retry logic", request.PhoneNumber);

            // Attempt to send with retry logic
            var result = await _smsRetryService.SendWithRetryAsync(notification.Id, cancellationToken);

            // Reload notification to get updated status
            var updatedNotification = await _context.Notifications
                .FindAsync(new object[] { notification.Id }, cancellationToken);

            return new TestSendSmsResponse
            {
                Success = result.Success,
                MessageId = result.MessageId,
                NotificationId = notification.Id,
                Status = updatedNotification?.Status ?? "unknown",
                ErrorMessage = result.ErrorMessage,
                RetryCount = updatedNotification?.RetryCount ?? 0,
                MaxRetries = updatedNotification?.MaxRetries ?? 3,
                NextRetryAt = updatedNotification?.NextRetryAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {PhoneNumber}", request.PhoneNumber);

            return new TestSendSmsResponse
            {
                Success = false,
                NotificationId = notification.Id,
                Status = "failed",
                ErrorMessage = ex.Message,
                RetryCount = notification.RetryCount,
                MaxRetries = notification.MaxRetries
            };
        }
    }
}
