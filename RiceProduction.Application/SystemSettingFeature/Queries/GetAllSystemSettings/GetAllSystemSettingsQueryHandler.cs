using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;

public class GetAllSystemSettingsQueryHandler : IRequestHandler<GetAllSystemSettingsQuery, PagedResult<List<SystemSettingResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAllSystemSettingsQueryHandler> _logger;

    public GetAllSystemSettingsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAllSystemSettingsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<SystemSettingResponse>>> Handle(GetAllSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _unitOfWork.Repository<SystemSetting>().GetQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.SearchKey))
            {
                query = query.Where(s => s.SettingKey.Contains(request.SearchKey) || 
                                        s.SettingDescription.Contains(request.SearchKey));
            }

            if (!string.IsNullOrEmpty(request.Category))
            {
                query = query.Where(s => s.SettingCategory == request.Category);
            }

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply sorting
            query = query.OrderBy(s => s.SettingCategory).ThenBy(s => s.SettingKey);

            // Apply pagination
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

