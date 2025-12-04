using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.FarmerFeature.Events;

public class SendFarmerWelcomeEmailsEventHandler : INotificationHandler<FarmersImportedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendFarmerWelcomeEmailsEventHandler> _logger;

    public SendFarmerWelcomeEmailsEventHandler(
        IEmailService emailService,
        ILogger<SendFarmerWelcomeEmailsEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(FarmersImportedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var farmersWithEmail = notification.ImportedFarmers
                .Where(f => !string.IsNullOrWhiteSpace(f.Email))
                .ToList();

            if (!farmersWithEmail.Any())
            {
                _logger.LogInformation("No farmers with email addresses to send welcome messages");
                return;
            }

            _logger.LogInformation("Sending welcome emails to {Count} farmers", farmersWithEmail.Count);

            var emailRequests = new List<SimpleEmailRequest>();
            
            foreach (var farmer in farmersWithEmail)
            {
                var templateData = new
                {
                    FullName = farmer.FullName,
                    PhoneNumber = farmer.PhoneNumber,
                    TempPassword = farmer.TempPassword
                };

                var htmlBody = GetEmailTemplate(templateData);

                emailRequests.Add(new SimpleEmailRequest
                {
                    To = farmer.Email!,
                    Subject = "Tài khoản của bạn đã được tạo - Hệ thống Quản lý Sản xuất Lúa",
                    HtmlBody = htmlBody,
                    Priority = 1
                });
            }

            var emailResult = await _emailService.SendBulkEmailAsync(emailRequests, cancellationToken);
            
            if (emailResult.Succeeded)
            {
                _logger.LogInformation(
                    "Successfully sent welcome emails. Batch ID: {BatchId}, Sent: {Sent}, Failed: {Failed}",
                    emailResult.Data?.Id,
                    emailResult.Data?.SentCount,
                    emailResult.Data?.FailedCount);
            }
            else
            {
                _logger.LogWarning("Failed to send welcome emails: {Errors}", 
                    string.Join(", ", emailResult.Errors ?? new List<string>()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending welcome emails to farmers");
        }
    }

    private string GetEmailTemplate(object templateData)
    {
        var template = @"
            <html>
            <head>
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                    .header { background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }
                    .content { background-color: #f9f9f9; padding: 30px; border: 1px solid #ddd; }
                    .credentials { background-color: #fff; padding: 15px; border-left: 4px solid #4CAF50; margin: 20px 0; }
                    .credentials strong { color: #4CAF50; }
                    .warning { background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }
                    .footer { background-color: #333; color: white; padding: 15px; text-align: center; border-radius: 0 0 5px 5px; font-size: 12px; }
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>Chào mừng đến với Hệ thống Quản lý Sản xuất Lúa</h1>
                    </div>
                    <div class='content'>
                        <p>Xin chào <strong>{{FullName}}</strong>,</p>
                        <p>Tài khoản của bạn đã được tạo thành công trong Hệ thống Quản lý Sản xuất Lúa.</p>
                        
                        <div class='credentials'>
                            <h3>Thông tin đăng nhập:</h3>
                            <p><strong>Số điện thoại (Tài khoản):</strong> {{PhoneNumber}}</p>
                            <p><strong>Mật khẩu tạm thời:</strong> {{TempPassword}}</p>
                        </div>

                        <div class='warning'>
                            <h3> Lưu ý quan trọng về bảo mật:</h3>
                            <ul>
                                <li><strong>Vui lòng đổi mật khẩu ngay sau khi đăng nhập lần đầu</strong></li>
                                <li>Không chia sẻ mật khẩu với bất kỳ ai</li>
                                <li>Chọn mật khẩu mạnh có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt</li>
                                <li>Không sử dụng mật khẩu giống với các tài khoản khác</li>
                            </ul>
                        </div>

                        <p>Nếu bạn gặp bất kỳ vấn đề gì khi đăng nhập hoặc cần hỗ trợ, vui lòng liên hệ với quản trị viên hệ thống.</p>
                        
                        <p>Trân trọng,<br><strong>Đội ngũ Hệ thống Quản lý Sản xuất Lúa</strong></p>
                    </div>
                    <div class='footer'>
                        <p>&copy; 2025 Rice Production Management System. All rights reserved.</p>
                        <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                    </div>
                </div>
            </body>
            </html>";

        var json = System.Text.Json.JsonSerializer.Serialize(templateData);
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        var result = template;
        if (dict != null)
        {
            foreach (var kvp in dict)
            {
                result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
            }
        }

        return result;
    }
}

