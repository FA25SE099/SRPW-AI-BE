using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;

namespace RiceProduction.Application.SystemSettingFeature.Queries.GetSystemSettingById;

public class GetSystemSettingByIdQueryHandler : IRequestHandler<GetSystemSettingByIdQuery, Result<SystemSettingResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetSystemSettingByIdQueryHandler> _logger;

    public GetSystemSettingByIdQueryHandler(IApplicationDbContext context, ILogger<GetSystemSettingByIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SystemSettingResponse>> Handle(GetSystemSettingByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SettingId, cancellationToken);

            if (setting == null)
            {
                return Result<SystemSettingResponse>.Failure($"System setting with ID {request.SettingId} not found");
            }

            var response = new SystemSettingResponse
            {
                Id = setting.Id,
                SettingKey = setting.SettingKey,
                SettingValue = setting.SettingValue,
                SettingCategory = setting.SettingCategory,
                SettingDescription = setting.SettingDescription,
                CreatedAt = setting.CreatedAt,
                LastModifiedAt = setting.LastModified
            };

            return Result<SystemSettingResponse>.Success(response, "Successfully retrieved system setting.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system setting with ID: {SettingId}", request.SettingId);
            return Result<SystemSettingResponse>.Failure("An error occurred while retrieving system setting");
        }
    }
}

