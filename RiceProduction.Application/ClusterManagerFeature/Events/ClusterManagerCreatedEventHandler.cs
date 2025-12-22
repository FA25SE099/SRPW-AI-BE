using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.SupervisorFeature.Events.SendEmailEventSupervisor;

namespace RiceProduction.Application.ClusterManagerFeature.Events
{
    public class ClusterManagerCreatedEventHandler : INotificationHandler<ClusterManagerCreatedEvent>
    {
        private readonly ILogger<SupervisorCreatedEventHandler> _logger;
        private readonly IEmailService _emailService;
        public ClusterManagerCreatedEventHandler(IEmailService emailService, ILogger<SupervisorCreatedEventHandler> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task Handle(ClusterManagerCreatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notification.Email))
                {
                    _logger.LogError("Email: {Email} not found", notification.Email);
                    return;
                }
                _logger.LogInformation("Sending email to user: {FullName} with id:{ClusterManagerId}", notification.FullName, notification.ClusterManagerId);
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
                        "Successfully sent email to farmer {ClusterManagerId} at {Email}",
                        notification.ClusterManagerId,
                        notification.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email to farmer {ClusterManagerId}: {Error}",
                        notification.ClusterManagerId,
                        email.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                   ex,
                   "Error sending email to farmer {ClusterManagerId}",
                   notification.ClusterManagerId);
            }
        }
    }
}
