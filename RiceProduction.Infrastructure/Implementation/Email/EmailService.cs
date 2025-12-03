using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace RiceProduction.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SmtpClient _smtpClient;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger,
            IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _logger = logger;
            _unitOfWork = unitOfWork;

            // Configure SMTP client for Firebase/Gmail
            _smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(
                    _configuration["Email:SenderEmail"],
                    _configuration["Email:AppPassword"]
                ),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
        }

        public async Task<Result<string>> SendEmailAsync(string to, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromEmail = _configuration["Email:SenderEmail"];
                var fromName = _configuration["Email:SenderName"] ?? "Rice Production System";

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = "Notification from Rice Production System",
                    IsBodyHtml = !string.IsNullOrEmpty(htmlBody)
                };

                mailMessage.To.Add(to);
                mailMessage.Body = !string.IsNullOrEmpty(htmlBody) ? htmlBody : textBody ?? "";

                if (!string.IsNullOrEmpty(textBody) && !string.IsNullOrEmpty(htmlBody))
                {
                    var plainView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
                    var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
                    mailMessage.AlternateViews.Add(plainView);
                    mailMessage.AlternateViews.Add(htmlView);
                }

                await _smtpClient.SendMailAsync(mailMessage, cancellationToken);

                var messageId = Guid.NewGuid().ToString();
                _logger.LogInformation("Email sent successfully to {To}, MessageId: {MessageId}", to, messageId);

                return Result<string>.Success(messageId, "Email sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}", to);
                return Result<string>.Failure($"Failed to send email: {ex.Message}");
            }
        }

        public async Task<Result<string>> SendEmailNotificationAsync(string to, string fullName, string resetToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var resetUrl = $"{_configuration["App:BaseUrl"]}/reset-password?token={resetToken}";

                var htmlBody = $@"
                <html>
                <body>
                <h2>Password Reset Request</h2>
                <p>Dear {fullName},</p>
                <p>Your password is: <strong>farmer@123</strong></p>
                </body>
                </html>";

                var textBody = $@"
                    Password Reset Request
                    
                    Dear {fullName},
                    
                    You have requested to reset your password. Please visit the following link to reset your password:
                    {resetUrl}
                    
                    This link will expire in 24 hours.
                    
                    If you did not request this password reset, please ignore this email.
                    
                    Best regards,
                    Rice Production System";

                var fromEmail = _configuration["Email:SenderEmail"];
                var fromName = _configuration["Email:SenderName"] ?? "Rice Production System";

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = "Password Reset Request",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                var plainView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
                mailMessage.AlternateViews.Add(plainView);
                mailMessage.AlternateViews.Add(htmlView);

                await _smtpClient.SendMailAsync(mailMessage, cancellationToken);

                var messageId = Guid.NewGuid().ToString();
                _logger.LogInformation("Password reset email sent successfully to {To}, MessageId: {MessageId}", to, messageId);

                return Result<string>.Success(messageId, "Password reset email sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {To}", to);
                return Result<string>.Failure($"Failed to send password reset email: {ex.Message}");
            }
        }

        public async Task<Result<EmailBatch>> SendBulkEmailAsync(List<SimpleEmailRequest> requests, CancellationToken cancellationToken = default)
        {
            var batch = new EmailBatch
            {
                BatchName = $"Bulk Email {DateTime.UtcNow:yyyyMMdd-HHmmss}",
                TotalEmails = requests.Count,
                Status = "processing",
                StartedAt = DateTime.UtcNow,
                EmailType = requests.FirstOrDefault()?.EmailType ?? "bulk",
                MaxConcurrency = 5,
                MaxRetries = 3
            };

            try
            {
                await _unitOfWork.Repository<EmailBatch>().AddAsync(batch);
                await _unitOfWork.CompleteAsync();

                var emailRequests = new List<EmailRequest>();
                var sentCount = 0;
                var failedCount = 0;

                foreach (var request in requests)
                {
                    var emailRequest = new EmailRequest
                    {
                        BatchId = batch.Id,
                        To = request.To,
                        Cc = request.Cc,
                        Bcc = request.Bcc,
                        Subject = request.Subject,
                        TextBody = request.TextBody ?? "",
                        HtmlBody = request.HtmlBody,
                        EmailType = request.EmailType,
                        Campaign = request.Campaign,
                        Priority = request.Priority,
                        ScheduledAt = request.ScheduledAt,
                        Status = "pending",
                        MaxRetries = batch.MaxRetries
                    };

                    try
                    {
                        var result = await SendEmailAsync(request.To, request.HtmlBody!, request.TextBody, cancellationToken);

                        if (result.Succeeded)
                        {
                            emailRequest.Status = "sent";
                            emailRequest.SentAt = DateTime.UtcNow;
                            emailRequest.MessageId = result.Data;
                            sentCount++;
                        }
                        else
                        {
                            emailRequest.Status = "failed";
                            emailRequest.ErrorMessage = result.Message;
                            failedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        emailRequest.Status = "failed";
                        emailRequest.ErrorMessage = ex.Message;
                        failedCount++;
                        _logger.LogError(ex, "Failed to send email in batch to {To}", request.To);
                    }

                    emailRequests.Add(emailRequest);
                }

                await _unitOfWork.Repository<EmailRequest>().AddRangeAsync(emailRequests);

                batch.SentCount = sentCount;
                batch.FailedCount = failedCount;
                batch.Status = failedCount == 0 ? "completed" : "partially_failed";
                batch.CompletedAt = DateTime.UtcNow;

                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Bulk email batch {BatchId} completed: {Sent} sent, {Failed} failed",
                    batch.Id, sentCount, failedCount);

                return Result<EmailBatch>.Success(batch, $"Bulk email completed: {sentCount} sent, {failedCount} failed");
            }
            catch (Exception ex)
            {
                batch.Status = "failed";
                batch.CompletedAt = DateTime.UtcNow;
                batch.ErrorMessage = ex.Message;

                await _unitOfWork.CompleteAsync();

                _logger.LogError(ex, "Bulk email batch {BatchId} failed", batch.Id);
                return Result<EmailBatch>.Failure($"Bulk email batch failed: {ex.Message}");
            }
        }

        public async Task<Result<string>> SendEmailWithTemplateAsync(string to, string subject, string templateName, object templateData, CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple template replacement - you can enhance this with a proper template engine
                var templateContent = await GetEmailTemplateAsync(templateName);
                var processedContent = ProcessTemplate(templateContent, templateData);

                return await SendEmailAsync(to, processedContent, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send templated email to {To} with template {Template}", to, templateName);
                return Result<string>.Failure($"Failed to send templated email: {ex.Message}");
            }
        }

        private async Task<string> GetEmailTemplateAsync(string templateName)
        {
            // This is a simple implementation - you can enhance it to load from files or database
            var templates = new Dictionary<string, string>
            {
                ["welcome"] = @"
                    <html><body>
                        <h2>Welcome {{FullName}}!</h2>
                        <p>Thank you for joining Rice Production System.</p>
                    </body></html>",
                ["task_assigned"] = @"
                    <html><body>
                        <h2>New Task Assigned</h2>
                        <p>Dear {{FullName}},</p>
                        <p>You have been assigned a new task: {{TaskName}}</p>
                        <p>Due date: {{DueDate}}</p>
                    </body></html>"
            };

            return await Task.FromResult(templates.GetValueOrDefault(templateName, "<p>Template not found</p>"));
        }

        private string ProcessTemplate(string template, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

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

        public void Dispose()
        {
            _smtpClient?.Dispose();
        }
    }
}
