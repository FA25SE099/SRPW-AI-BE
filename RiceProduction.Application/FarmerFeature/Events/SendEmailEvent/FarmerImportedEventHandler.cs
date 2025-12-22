using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Events;

public class FarmerImportedEventHandler : INotificationHandler<FarmersImportedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<FarmerImportedEventHandler> _logger;

    public FarmerImportedEventHandler(
        IEmailService emailService,
        ILogger<FarmerImportedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(FarmersImportedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var farmersWithEmail = notification.ImportedFarmers
                .Where(f => !string.IsNullOrEmpty(f.Email))
                .ToList();
            if (!farmersWithEmail.Any())
            {
                _logger.LogInformation("No farmers with email addresses in the import batch");
                return;
            }
            _logger.LogInformation(
                  "Preparing to send emails to {Count} imported farmers",
                  farmersWithEmail.Count);
            var emailRequests = new List<SimpleEmailRequest>();
            foreach (var farmer in farmersWithEmail)
            {
                // Use email as account (not phone number)
                var htmlBody = GetFarmerAccountEmailHtml(
                    farmer.FullName,
                    farmer.Email!, // Pass email as account
                    farmer.TempPassword);

                var textBody = GetFarmerAccountEmailText(
                    farmer.FullName,
                    farmer.Email!, // Pass email as account
                    farmer.TempPassword);

                var emailRequest = new SimpleEmailRequest
                {
                    To = farmer.Email!,
                    Subject = "Tài khoản Rice Production System của bạn đã được tạo",
                    HtmlBody = htmlBody,
                    TextBody = textBody,
                    EmailType = "account_creation",
                    Campaign = "farmer_import",
                    Priority = 0
                };

                emailRequests.Add(emailRequest);
            }

            // Actually send the emails
            if (emailRequests.Any())
            {
                var result = await _emailService.SendBulkEmailAsync(emailRequests, cancellationToken);

                if (result.Succeeded)
                {
                    _logger.LogInformation(
                        "Successfully sent bulk emails. Batch ID: {BatchId}, Sent: {Sent}, Failed: {Failed}",
                        result.Data?.Id,
                        result.Data?.SentCount,
                        result.Data?.FailedCount);
                }
                else
                {
                    _logger.LogWarning("Failed to send bulk emails: {Errors}",
                        string.Join(", ", result.Errors ?? new List<string>()));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending bulk emails for imported farmers");
        }
    }

    private string GetFarmerAccountEmailHtml(string fullName, string email, string password)
    {
        return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }}
                        .credentials {{ background-color: #fff; padding: 15px; border-left: 4px solid #4CAF50; margin: 20px 0; }}
                        .credentials strong {{ color: #4CAF50; }}
                        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
                        .footer {{ background-color: #333; color: white; padding: 15px; text-align: center; border-radius: 0 0 5px 5px; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Chào mừng đến với Hệ thống Quản lý Sản xuất Lúa</h1>
                        </div>
                        <div class='content'>
                            <p>Xin chào <strong>{fullName}</strong>,</p>
                            <p>Tài khoản của bạn đã được tạo thành công trong Hệ thống Quản lý Sản xuất Lúa.</p>
                            
                            <div class='credentials'>
                                <h3>Thông tin đăng nhập:</h3>
                                <p><strong>Số tài khoản:</strong> {email}</p>
                                <p><strong>Mật khẩu tạm thời:</strong> {password}</p>
                            </div>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2025 Rice Production Management System. All rights reserved.</p>
                            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                        </div>
                    </div>
                </body>
                </html>";
    }

    private string GetFarmerAccountEmailText(string fullName, string email, string password)
    {
        return $@"
        Chào mừng đến với Hệ thống Quản lý Sản xuất Lúa

        Xin chào {fullName},

        Tài khoản của bạn đã được tạo thành công trong Hệ thống Quản lý Sản xuất Lúa.

        THÔNG TIN ĐĂNG NHẬP:
        - Số tài khoản: {email}
        - Mật khẩu tạm thời: {password}";

    }
}
