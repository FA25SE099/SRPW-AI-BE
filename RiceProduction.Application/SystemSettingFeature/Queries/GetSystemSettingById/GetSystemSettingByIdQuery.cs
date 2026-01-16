using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;

namespace RiceProduction.Application.SystemSettingFeature.Queries.GetSystemSettingById;

public class GetSystemSettingByIdQuery : IRequest<Result<SystemSettingResponse>>, ICacheable
{
    public Guid SettingId { get; set; }
    
    public bool BypassCache { get; init; } = false;
    public string CacheKey => $"SystemSetting:{SettingId}";
    public int SlidingExpirationInMinutes => 60;
    public int AbsoluteExpirationInMinutes => 120;
}

