using System.Text.Json.Serialization;

namespace RiceProduction.Application.SmsFeature.Commands.ProcessSmsDeliveryWebhook;

/// <summary>
/// SpeedSMS webhook request model
/// </summary>
public class SpeedSmsWebhookRequest
{
    /// <summary>
    /// Type of webhook: "report" for delivery report, or "incoming" for incoming SMS
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Transaction ID from SpeedSMS API response
    /// </summary>
    [JsonPropertyName("tranId")]
    public string TranId { get; set; } = string.Empty;

    /// <summary>
    /// Phone number that received the message
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status:
    /// status = 0: success
    /// 0 &lt; status &lt; 64: temporary fail
    /// status >= 64: failed
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }
}

/// <summary>
/// Response for webhook processing
/// </summary>
public class WebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? NotificationId { get; set; }
}
