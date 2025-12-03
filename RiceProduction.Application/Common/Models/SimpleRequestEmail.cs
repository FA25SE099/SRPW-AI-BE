namespace RiceProduction.Application.Common.Models
{
    public class SimpleEmailRequest
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
        public object? TemplateData { get; set; }
    }
}
