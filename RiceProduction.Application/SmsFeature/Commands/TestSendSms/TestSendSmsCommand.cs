using MediatR;

namespace RiceProduction.Application.SmsFeature.Commands.TestSendSms;

public record TestSendSmsCommand : IRequest<TestSendSmsResponse>
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string RecipientId { get; init; } = string.Empty;
}

public class TestSendSmsResponse
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public Guid NotificationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? NextRetryAt { get; set; }
}
