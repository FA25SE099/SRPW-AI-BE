using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Events;
using static RiceProduction.Application.FarmerFeature.Events.SendEmailEvent.FarmerImportedEvent;

namespace RiceProduction.Infrastructure.Services
{
    public class EmailJobService : IEmailJobService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailJobService> _logger;

        public EmailJobService(
            IEmailService emailService,
            ILogger<EmailJobService> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendBulkFarmerAccountEmailsAsync(
            List<ImportedFarmerInfo> farmers,
            CancellationToken cancellationToken = default)
        {
            var farmersWithEmail = farmers
                .Where(f => !string.IsNullOrWhiteSpace(f.Email))
                .ToList();

            if (!farmersWithEmail.Any())
            {
                _logger.LogInformation("No farmers with email addresses to send");
                return;
            }

            _logger.LogInformation("Sending bulk emails to {Count} farmers", farmersWithEmail.Count);

            var emailRequests = new List<SimpleEmailRequest>();

            foreach (var farmer in farmersWithEmail)
            {
                var htmlBody = GetFarmerAccountEmailHtml(farmer.FullName, farmer.Email!, farmer.Password);
                var textBody = GetFarmerAccountEmailText(farmer.FullName, farmer.Email!, farmer.Password);

                emailRequests.Add(new SimpleEmailRequest
                {
                    To = farmer.Email!,
                    Subject = "Tài khoản Rice Production System của bạn đã được tạo",
                    HtmlBody = htmlBody,
                    TextBody = textBody,
                    EmailType = "account_creation",
                    Campaign = "farmer_import",
                    Priority = 0
                });
            }

            var result = await _emailService.SendBulkEmailAsync(emailRequests, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Bulk email completed. Batch ID: {BatchId}, Sent: {Sent}, Failed: {Failed}",
                    result.Data?.Id,
                    result.Data?.SentCount,
                    result.Data?.FailedCount);
            }
            else
            {
                _logger.LogWarning("Bulk email completed with errors: {Message}", result.Message);
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
                            <p><strong>Email (Tài khoản):</strong> {email}</p>
                            <p><strong>Mật khẩu tạm thời:</strong> {password}</p>
                        </div>

                        <div class='warning'>
                            <h3>⚠️ Lưu ý quan trọng về bảo mật:</h3>
                            <ul>
                                <li><strong>Vui lòng đổi mật khẩu ngay sau khi đăng nhập lần đầu</strong></li>
                                <li>Không chia sẻ mật khẩu với bất kỳ ai</li>
                                <li>Chọn mật khẩu mạnh có ít nhất 8 ký tự</li>
                            </ul>
                        </div>

                        <p>Trân trọng,<br><strong>Đội ngũ Hệ thống Quản lý Sản xuất Lúa</strong></p>
                    </div>
                    <div class='footer'>
                        <p>&copy; 2025 Rice Production Management System. All rights reserved.</p>
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

Tài khoản của bạn đã được tạo thành công.

THÔNG TIN ĐĂNG NHẬP:
- Email (Tài khoản): {email}
- Mật khẩu tạm thời: {password}

⚠️ Vui lòng đổi mật khẩu ngay sau khi đăng nhập lần đầu.

Trân trọng,
Đội ngũ Hệ thống Quản lý Sản xuất Lúa";
        }
    }
}