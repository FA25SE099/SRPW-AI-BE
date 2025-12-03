using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.EmailFeature.Commands.SendEmail
{
    public class SendEmailCommand : IRequest<Result<string>>
    {
        public string To { get; set; } = string.Empty;
        public string? Cc { get; set; }
        public string? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? TextBody { get; set; }
        public string? HtmlBody { get; set; }
        public string EmailType { get; set; } = "general";
        public string? Campaign { get; set; }
        public int Priority { get; set; } = 0;
        public DateTime? ScheduledAt { get; set; }
        public string? TemplateName { get; set; }
        public object? TemplateData { get; set; }
        public bool SaveToDatabase { get; set; } = true;
    }
}
