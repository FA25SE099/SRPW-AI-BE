using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.UavVendorFeature.Events
{
    public class UavVendorCreatedEventHandler : INotificationHandler<UavVendorCreatedEvent>
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<UavVendorCreatedEventHandler> _logger;

        public UavVendorCreatedEventHandler(IEmailService emailService, ILogger<UavVendorCreatedEventHandler> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task Handle(UavVendorCreatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notification.Email))
                {
                    _logger.LogError("Email: {Email} not found", notification.Email);
                    return;
                }
                _logger.LogInformation("Sending email to user: {FullName} with id: {UavVendorId}", notification.FullName, notification.UavVendorId);
                var templateData = new
                {
                    FullName = notification.FullName,
                    Email = notification.Email,
                    TempPassword = notification.Password,
                };
                var email = await _emailService.SendEmailWithTemplateAsync(
                    to: notification.Email,
                        templateData: templateData,
                        templateName: "user_account_created",
                        subject: "Tài khoản của bạn đã được khởi tạo",
                        cancellationToken: cancellationToken
                    );
                if (email.Succeeded)
                {
                    _logger.LogInformation(
                        "Successfully sent email to farmer {UavVendorId} at {Email}",
                        notification.UavVendorId,
                        notification.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email to farmer {UavVendorId}: {Error}",
                        notification.UavVendorId,
                        email.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                  ex,
                  "Error sending email to farmer {UavVendorId}",
                  notification.UavVendorId);
            }
        }
    }
}
