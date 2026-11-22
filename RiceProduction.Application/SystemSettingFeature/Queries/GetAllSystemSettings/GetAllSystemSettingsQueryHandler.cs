using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;

public class GetAllSystemSettingsQueryHandler : IRequestHandler<GetAllSystemSettingsQuery, PagedResult<List<SystemSettingResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllSystemSettingsQueryHandler> _logger;

    public GetAllSystemSettingsQueryHandler(IApplicationDbContext context, ILogger<GetAllSystemSettingsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<SystemSettingResponse>>> Handle(GetAllSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.SystemSettings.AsNoTracking();

            if (!string.IsNullOrEmpty(request.SearchKey))
            {
                query = query.Where(s => s.SettingKey.Contains(request.SearchKey) || 
                                        s.SettingDescription.Contains(request.SearchKey));
            }

            if (!string.IsNullOrEmpty(request.Category))
            {
                query = query.Where(s => s.SettingCategory == request.Category);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            query = query.OrderBy(s => s.SettingCategory).ThenBy(s => s.SettingKey);

            var skip = (request.CurrentPage - 1) * request.PageSize;
            var settings = await query
                .Skip(skip)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var settingResponses = settings.Select(s => new SystemSettingResponse
            {
                Id = s.Id,
                SettingKey = s.SettingKey,
                SettingValue = s.SettingValue,
                SettingCategory = s.SettingCategory,
                SettingDescription = s.SettingDescription,
                CreatedAt = s.CreatedAt,
            }).ToList();

            var pagedResult = PagedResult<List<SystemSettingResponse>>.Success(
                settingResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved system settings.");

            return pagedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all system settings");
            return PagedResult<List<SystemSettingResponse>>.Failure("An error occurred while retrieving system settings");
        }
    }
}

