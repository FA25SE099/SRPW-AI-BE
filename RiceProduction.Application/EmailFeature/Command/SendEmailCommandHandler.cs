using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.EmailFeature.Commands.SendEmail
{
    public class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, Result<string>>
    {
        private readonly IEmailService _emailService;
        private readonly IApplicationDbContext _context;
        private readonly ILogger<SendEmailCommandHandler> _logger;

        public SendEmailCommandHandler(
            IEmailService emailService,
            IApplicationDbContext context,
            ILogger<SendEmailCommandHandler> logger)
        {
            _emailService = emailService;
            _context = context;
            _logger = logger;
        }

        public async Task<Result<string>> Handle(SendEmailCommand request, CancellationToken cancellationToken)
        {
            EmailRequest? emailRequest = null;

            try
            {
                if (request.SaveToDatabase)
                {
                    emailRequest = new EmailRequest
                    {
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
                        MaxRetries = 3
                    };

                    _context.EmailRequests.Add(emailRequest);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                Result<string> result;

                if (!string.IsNullOrEmpty(request.TemplateName) && request.TemplateData != null)
                {
                    result = await _emailService.SendEmailWithTemplateAsync(
                        request.To,
                        request.Subject,
                        request.TemplateName,
                        request.TemplateData,
                        cancellationToken);
                }
                else
                {
                    result = await _emailService.SendEmailAsync(
                        request.To,
                        request.HtmlBody!,
                        request.TextBody,
                        cancellationToken);
                }

                if (emailRequest != null)
                {
                    if (result.Succeeded)
                    {
                        emailRequest.Status = "sent";
                        emailRequest.SentAt = DateTime.UtcNow;
                        emailRequest.MessageId = result.Data;
                    }
                    else
                    {
                        emailRequest.Status = "failed";
                        emailRequest.ErrorMessage = result.Message;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                }

                if (result.Succeeded)
                {
                    _logger.LogInformation("Email sent successfully to {To}, MessageId: {MessageId}",
                        request.To, result.Data);
                }
                else
                {
                    _logger.LogError("Failed to send email to {To}: {Error}", request.To, result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", request.To);

                if (emailRequest != null)
                {
                    try
                    {
                        emailRequest.Status = "failed";
                        emailRequest.ErrorMessage = ex.Message;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update email request status");
                    }
                }

                return Result<string>.Failure($"An error occurred while sending email: {ex.Message}");
            }
        }
    }
}
