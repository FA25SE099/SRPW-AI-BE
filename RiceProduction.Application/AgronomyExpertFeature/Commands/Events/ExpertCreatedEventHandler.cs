using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.SupervisorFeature.Events.SendEmailEventSupervisor;

namespace RiceProduction.Application.AgronomyExpertFeature.Commands.Events
{
    public class ExpertCreatedEventHandler : INotificationHandler<ExpertCreatedEvent>
    {
        private readonly ILogger<ExpertCreatedEventHandler> _logger;
        private readonly IEmailService _emailService;
        public ExpertCreatedEventHandler(IEmailService emailService, ILogger<ExpertCreatedEventHandler> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }
        public async Task Handle(ExpertCreatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notification.Email))
                {
                    _logger.LogError("Email: {Email} not found", notification.Email);
                    return;
                }
                _logger.LogInformation("Sending email to user: {FullName} with id:{ExpertId}", notification.FullName, notification.ExpertId);
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
                        "Successfully sent email to farmer {ExpertId} at {Email}",
                        notification.ExpertId,
                        notification.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email to farmer {ExpertId}: {Error}",
                        notification.ExpertId,
                        email.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                   ex,
                   "Error sending email to farmer {ExpertId}",
                   notification.ExpertId);
            }
        }
    }
}
