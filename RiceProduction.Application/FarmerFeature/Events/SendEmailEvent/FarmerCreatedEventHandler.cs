using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.FarmerFeature.Events.SendEmailEvent
{
    public class FarmerCreatedEventHandler : INotificationHandler<FarmerCreatedEvent>
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<FarmerCreatedEventHandler> _logger;

        public FarmerCreatedEventHandler(IEmailService emailService, ILogger<FarmerCreatedEventHandler> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task Handle(FarmerCreatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(notification.Email))
                {
                    _logger.LogInformation("Farmer {FarmerId}: Email not found", notification.FarmerId);
                    return;
                }
                _logger.LogInformation(
                    "Sending account credentials email to farmer {FarmerId} at {Email}",
                    notification.FarmerId,
                    notification.Email);
                var templateData = new
                {
                    FullName = notification.FullName,
                    Email = notification.Email, 
                    TempPassword = notification.Password,
                };
                var result = await _emailService.SendEmailWithTemplateAsync(
                    to: notification.Email,
                    templateData: templateData,
                    templateName: "user_account_created",
                    subject: "Tài khoản của bạn đã được khởi tạo",
                    cancellationToken: cancellationToken);
                if (result.Succeeded)
                {
                    _logger.LogInformation(
                        "Successfully sent email to farmer {FarmerId} at {Email}",
                        notification.FarmerId,
                        notification.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email to farmer {FarmerId}: {Error}",
                        notification.FarmerId,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending email to farmer {FarmerId}",
                    notification.FarmerId);
            }
        }
    }
}
