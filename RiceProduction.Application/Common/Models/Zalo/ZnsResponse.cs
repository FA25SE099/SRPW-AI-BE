namespace RiceProduction.Application.Common.Models.Zalo;

public class ZnsResponse
{
    public int Error { get; set; }
    public string Message { get; set; } = string.Empty;
    public ZnsResponseData? Data { get; set; }
}

public class ZnsResponseData
{
    public string MsgId { get; set; } = string.Empty;
    public string SentTime { get; set; } = string.Empty;
    public string SendingMode { get; set; } = string.Empty;
    public ZnsQuota? Quota { get; set; }
}

public class ZnsQuota
{
    public string DailyQuota { get; set; } = string.Empty;
    public string RemainingQuota { get; set; } = string.Empty;
}
