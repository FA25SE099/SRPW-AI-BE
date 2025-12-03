using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.NotificationFeature.Command.PushNotification
{
    public class SendPushNotificationCommandHandler : IRequestHandler<SendPushNotificationCommand, SendPushNotificationResult>
    {
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SendPushNotificationCommandHandler> _logger;

        public SendPushNotificationCommandHandler(INotificationService notificationService, IUnitOfWork unitOfWork, ILogger<SendPushNotificationCommandHandler> logger)
        {
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<SendPushNotificationResult> Handle(SendPushNotificationCommand request, CancellationToken cancellationToken)
        {
            var result = new SendPushNotificationResult
            {
                SentAt = DateTime.UtcNow,
                NotificationId = request.NotificationId,
            };

            var data = new Dictionary<string, string>(request.Data ?? new());
            if (!string.IsNullOrEmpty(request.UserId)) data["userId"] = request.UserId;
            if (!string.IsNullOrEmpty(request.Tag)) data["tag"] = request.Tag;
            if (request.NotificationId.HasValue) data["notificationId"] = request.NotificationId.Value.ToString();

            Notification? notification = null;
            if (request.SaveToDatabase)
            {
                notification = new Notification
                {
                    Id = request.NotificationId ?? Guid.NewGuid(),
                    RecipientId = request.UserId ?? "",
                    ActivityType = "push_sent",
                    ObjectType = "push",
                    TimeSent = DateTime.UtcNow,
                    IsUnread = true,
                    Title = request.Title,
                    Content = request.Body,
                    Status = "pending",
                    MaxRetries = 3,
                    Push = true,
                    PushStatus = "pending",
                    DataPayload = System.Text.Json.JsonSerializer.Serialize(data)
                };
                await _unitOfWork.Repository<Notification>().AddAsync(notification);
                await _unitOfWork.CompleteAsync();
            }
            try
            {
                _logger.LogInformation("Attempting to send push notification to token: {PushToken}", request.PushToken);
                bool success = await _notificationService.SendPushAsync(
                    request.PushToken,
                    request.Title,
                    request.Body,
                    data,
                    cancellationToken);
                result.Success = success;
                result.IsTokenValid = success;
                result.MessageId = success ? "sent" : null;

                if (notification != null)
                {
                    await _notificationService.UpdateNotification(notification, success, success ? null : "Push Failed");
                    var updateNotification = await _unitOfWork.Repository<Notification>().FindAsync(n => n.Id == notification.Id);
                    result.NotificationId = updateNotification?.Id ?? notification.Id;
                    result.ErrorMessage = updateNotification?.PushError;
                    result.RetryCount = updateNotification?.RetryCount ?? 0;
                    result.NextRetryAt = updateNotification?.NextRetryAt;
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to token: {PushToken}", request.PushToken);

                result.Success = false;
                result.IsTokenValid = false;
                result.ErrorMessage = ex.Message;

                if (notification != null)
                {
                    await _notificationService.UpdateNotification(notification, false, ex.Message);
                    var updatedNotification = await _unitOfWork.Repository<Notification>()
                        .FindAsync(n => n.Id == notification.Id);

                    result.NotificationId = updatedNotification?.Id ?? notification.Id;
                    result.RetryCount = updatedNotification?.RetryCount;
                    result.NextRetryAt = updatedNotification?.NextRetryAt;
                }
            }
            return result;
        }
    }
}
