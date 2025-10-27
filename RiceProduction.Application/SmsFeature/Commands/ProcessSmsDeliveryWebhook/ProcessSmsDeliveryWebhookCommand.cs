using MediatR;

namespace RiceProduction.Application.SmsFeature.Commands.ProcessSmsDeliveryWebhook;

public record ProcessSmsDeliveryWebhookCommand : IRequest<WebhookResponse>
{
    public string Type { get; init; } = string.Empty;
    public string TranId { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public int Status { get; init; }
}
