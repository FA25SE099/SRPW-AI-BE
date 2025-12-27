using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;

public class GetAllSystemSettingsQuery : IRequest<PagedResult<List<SystemSettingResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchKey { get; set; }
    public string? Category { get; set; }
}

public class SystemSettingResponse
{
    public Guid Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string SettingCategory { get; set; } = string.Empty;
    public string SettingDescription { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}

