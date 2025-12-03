using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task<Result<string>> SendEmailAsync(string to, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default);
        Task<Result<string>> SendEmailNotificationAsync(string to, string fullName, string resetToken, CancellationToken cancellationToken = default);
        Task<Result<EmailBatch>> SendBulkEmailAsync(List<SimpleEmailRequest> requests, CancellationToken cancellationToken = default);
        Task<Result<string>> SendEmailWithTemplateAsync(string to, string subject, string templateName, object templateData, CancellationToken cancellationToken = default);
    }
}
