namespace RiceProduction.Application.Common.Models.Request.SystemSettingRequests;

public class SystemSettingListRequest
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchKey { get; set; }
    public string? Category { get; set; }
}

public class UpdateSystemSettingRequest
{
    public string SettingValue { get; set; } = string.Empty;
    public string? SettingDescription { get; set; }
}

