public class ZnsRequest
{
    public string Phone { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public object TemplateData { get; set; } = new { }; // e.g., new { ky = "1", ... }
    public string? TrackingId { get; set; }
    public string? SendingMode { get; set; } = "1";
}

public class ZnsResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? SentTime { get; set; }
    public string? SendingMode { get; set; }
    public string? RemainingQuota { get; set; }
    public string? DailyQuota { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? TrackingId { get; set; }
    public string? ErrorMessage { get; set; }
}