using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SmsFeature.Commands.ProcessSmsDeliveryWebhook;

public class ProcessSmsDeliveryWebhookCommandHandler : IRequestHandler<ProcessSmsDeliveryWebhookCommand, WebhookResponse>
{
    private readonly IUnitOfWork _context;
    private readonly ILogger<ProcessSmsDeliveryWebhookCommandHandler> _logger;

    public ProcessSmsDeliveryWebhookCommandHandler(
        IUnitOfWork context,
        ILogger<ProcessSmsDeliveryWebhookCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WebhookResponse> Handle(ProcessSmsDeliveryWebhookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing SpeedSMS webhook - Type: {Type}, TranId: {TranId}, Phone: {Phone}, Status: {Status}",
                request.Type, request.TranId, request.Phone, request.Status);

            if (request.Type != "report")
            {
                _logger.LogWarning("Received non-report webhook type: {Type}", request.Type);
                return new WebhookResponse
                {
                    Success = true,
                    Message = $"Webhook type '{request.Type}' is not processed"
                };
            }

            // Find notification by MessageId (tranId from SpeedSMS)
            var notification = await _context.Repository<Notification>()
                .FindAsync(n => n.MessageId == request.TranId && n.PhoneNumber == request.Phone);

            if (notification == null)
            {
                _logger.LogWarning("Notification not found for TranId: {TranId}, Phone: {Phone}", request.TranId, request.Phone);
                return new WebhookResponse
                {
                    Success = false,
                    Message = $"Notification not found for TranId: {request.TranId}"
                };
            }

            string newStatus;
            string errorMessage = null;

            if (request.Status == 0)
            {
                newStatus = "delivered";
                _logger.LogInformation("SMS delivered successfully - TranId: {TranId}, Phone: {Phone}", request.TranId, request.Phone);
            }
            else if (request.Status > 0 && request.Status < 64)
            {
                // Temporary failure
                newStatus = "temporary_failed";
                errorMessage = $"Temporary delivery failure (status code: {request.Status})";
                _logger.LogWarning("SMS temporary failure - TranId: {TranId}, Phone: {Phone}, Status: {Status}", 
                    request.TranId, request.Phone, request.Status);
            }
            else
            {
                // Permanent failure (status >= 64)
                newStatus = "failed";
                errorMessage = $"Permanent delivery failure (status code: {request.Status}). Phone may be off, out of coverage, or blocked";
                _logger.LogError("SMS delivery failed - TranId: {TranId}, Phone: {Phone}, Status: {Status}", 
                    request.TranId, request.Phone, request.Status);
            }

            // Update notification
            notification.Status = newStatus;
            if (errorMessage != null)
            {
                notification.ErrorMessage = errorMessage;
            }
            notification.LastModified = DateTimeOffset.UtcNow;

            await _context.CompleteAsync();

            _logger.LogInformation("Notification updated successfully - Id: {NotificationId}, Status: {Status}", 
                notification.Id, newStatus);

            return new WebhookResponse
            {
                Success = true,
                Message = $"Notification status updated to: {newStatus}",
                NotificationId = notification.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SpeedSMS webhook - TranId: {TranId}", request.TranId);
            return new WebhookResponse
            {
                Success = false,
                Message = $"Error processing webhook: {ex.Message}"
            };
        }
    }
}
