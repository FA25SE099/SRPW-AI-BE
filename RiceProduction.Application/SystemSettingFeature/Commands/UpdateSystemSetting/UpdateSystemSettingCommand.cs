using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SystemSettingFeature.Commands.UpdateSystemSetting;

public class UpdateSystemSettingCommand : IRequest<Result<Guid>>
{
    public Guid SettingId { get; set; }
    public string SettingValue { get; set; } = string.Empty;
    public string? SettingDescription { get; set; }
}

