using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SystemSettingFeature.Commands.UpdateSystemSetting;

public class UpdateSystemSettingCommandHandler : IRequestHandler<UpdateSystemSettingCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSystemSettingCommandHandler> _logger;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateSystemSettingCommandHandler(
        IUnitOfWork unitOfWork, 
        ILogger<UpdateSystemSettingCommandHandler> logger,
        ICacheInvalidator cacheInvalidator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Result<Guid>> Handle(UpdateSystemSettingCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var settingRepo = _unitOfWork.Repository<SystemSetting>();

            var setting = await settingRepo.FindAsync(s => s.Id == request.SettingId);
            if (setting == null)
            {
                return Result<Guid>.Failure($"System setting with ID {request.SettingId} not found");
            }

            // Only allow updating the value and description, not the key or category
            setting.SettingValue = request.SettingValue;
            
            if (!string.IsNullOrEmpty(request.SettingDescription))
            {
                setting.SettingDescription = request.SettingDescription;
            }

            settingRepo.Update(setting);
            await _unitOfWork.CompleteAsync();

            // Invalidate cache for this setting and all settings list
            _cacheInvalidator.InvalidateCachesByPattern("SystemSettings:*");
            _cacheInvalidator.InvalidateCache($"SystemSetting:{request.SettingId}");

            _logger.LogInformation("Updated system setting: {SettingKey} = {SettingValue}", 
                setting.SettingKey, request.SettingValue);
            
            return Result<Guid>.Success(request.SettingId, "System setting updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system setting with ID: {SettingId}", request.SettingId);
            return Result<Guid>.Failure("Failed to update system setting");
        }
    }
}

