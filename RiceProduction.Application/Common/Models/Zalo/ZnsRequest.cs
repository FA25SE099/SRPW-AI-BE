namespace RiceProduction.Application.Common.Models.Zalo;

public class ZnsRequest
{
    public string Phone { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, string> TemplateData { get; set; } = new();
    public string? SendingMode { get; set; }
    public string TrackingId { get; set; } = string.Empty;
}
