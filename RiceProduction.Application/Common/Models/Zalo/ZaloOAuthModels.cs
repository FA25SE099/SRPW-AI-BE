namespace RiceProduction.Application.Common.Models.Zalo;

public class ZaloTokenRequest
{
    public string Code { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string GrantType { get; set; } = string.Empty;
    public string? CodeVerifier { get; set; }
}

public class ZaloRefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string GrantType { get; set; } = "refresh_token";
}

public class ZaloTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ExpiresIn { get; set; } = string.Empty;
}

public class ZaloAuthorizationUrlRequest
{
    public string AppId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class BulkZnsRequest
{
    public string Phone { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, string> TemplateData { get; set; } = new();
    public string TrackingId { get; set; } = string.Empty;
}

public class BulkZnsSendResult
{
    public string TrackingId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool Success { get; set; }
    public ZnsResponse? Response { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public class BulkZnsSummary
{
    public int TotalRequests { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkZnsSendResult> Results { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class BulkZnsProgress
{
    public int TotalRequests { get; set; }
    public int ProcessedRequests { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double ProgressPercentage => TotalRequests > 0 ? (ProcessedRequests * 100.0 / TotalRequests) : 0;
    public string? CurrentPhone { get; set; }
}
