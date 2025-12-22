using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Events.SendEmailEventSupervisor
{
    public class SupervisorCreatedEventHandler : INotificationHandler<SupervisorCreatedEvent>
    {
        private readonly ILogger<SupervisorCreatedEventHandler> _logger;
        private readonly IEmailService _emailService;
        public SupervisorCreatedEventHandler(IEmailService emailService, ILogger<SupervisorCreatedEventHandler> logger) 
        { 
            _logger = logger;
            _emailService = emailService;
        }
        public async Task Handle(SupervisorCreatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(notification.Email))
                {
                    _logger.LogError("Email: {Email} not found", notification.Email);
                    return;
                }
                _logger.LogInformation("Sending email to user: {FullName} with id:{SupervisorId}", notification.FullName, notification.SupervisorId);
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
                        "Successfully sent email to farmer {SupervisorId} at {Email}",
                        notification.SupervisorId,
                        notification.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email to farmer {SupervisorId}: {Error}",
                        notification.SupervisorId,
                        email.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                   ex,
                   "Error sending email to farmer {SupervisorId}",
                   notification.SupervisorId);
            }
        }
    }
}
