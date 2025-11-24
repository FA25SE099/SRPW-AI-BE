using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Queries.GetAllEmergencyProtocols;

public class GetAllEmergencyProtocolsQueryHandler : IRequestHandler<GetAllEmergencyProtocolsQuery, PagedResult<List<EmergencyProtocolDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAllEmergencyProtocolsQueryHandler> _logger;

    public GetAllEmergencyProtocolsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAllEmergencyProtocolsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<EmergencyProtocolDto>>> Handle(
        GetAllEmergencyProtocolsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting emergency protocols - Page: {Page}, PageSize: {PageSize}, CategoryId: {CategoryId}, Search: {Search}, IsActive: {IsActive}",
                request.CurrentPage, request.PageSize, request.CategoryId, request.SearchTerm, request.IsActive);

            // Start with base queryable
            var query = _unitOfWork.Repository<EmergencyProtocol>()
                .GetQueryable()
                .Include(ep => ep.Category)
                .Include(ep => ep.StandardPlanStages)
                    .ThenInclude(s => s.StandardPlanTasks)
                .Include(ep => ep.Thresholds)
                    .ThenInclude(t => t.PestProtocol)
                .Include(ep => ep.Thresholds)
                    .ThenInclude(t => t.WeatherProtocol)
                .AsQueryable();

            // Apply CategoryId filter
            if (request.CategoryId.HasValue)
            {
                query = query.Where(ep => ep.CategoryId == request.CategoryId.Value);
            }

            // Apply IsActive filter
            if (request.IsActive.HasValue)
            {
                query = query.Where(ep => ep.IsActive == request.IsActive.Value);
            }

            // Apply SearchTerm filter (search in PlanName, PestProtocol.Name, WeatherProtocol.Name)
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(ep =>
                    ep.PlanName.ToLower().Contains(searchTerm) ||
                    ep.Thresholds.Any(t =>
                        t.PestProtocol.Name.ToLower().Contains(searchTerm) ||
                        t.WeatherProtocol.Name.ToLower().Contains(searchTerm)
                    )
                );
            }

            // Apply ordering
            query = query.OrderByDescending(ep => ep.CreatedAt);

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var pagedProtocols = await query
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Manual mapping
            var emergencyProtocolDtos = pagedProtocols
                .Select(ep => new EmergencyProtocolDto
                {
                    Id = ep.Id,
                    Name = ep.PlanName,
                    Description = ep.Description,
                    CategoryId = ep.CategoryId,
                    CategoryName = ep.Category?.CategoryName ?? string.Empty,
                    TotalDuration = ep.TotalDurationDays,
                    IsActive = ep.IsActive,
                    TotalThresholds = ep.Thresholds?.Count ?? 0,
                    TotalStages = ep.StandardPlanStages?.Count ?? 0,
                    TotalTasks = ep.StandardPlanStages?.SelectMany(s => s.StandardPlanTasks).Count() ?? 0,
                    CreatedAt = ep.CreatedAt,
                    CreatedBy = ep.CreatedBy,
                    LastModified = ep.LastModified,
                    LastModifiedBy = ep.LastModifiedBy
                })
                .ToList();

            if (!emergencyProtocolDtos.Any())
            {
                return PagedResult<List<EmergencyProtocolDto>>.Success(
                    new List<EmergencyProtocolDto>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No emergency protocols found matching the criteria.");
            }

            _logger.LogInformation(
                "Retrieved {Count} emergency protocols out of {Total}",
                emergencyProtocolDtos.Count, totalCount);

            return PagedResult<List<EmergencyProtocolDto>>.Success(
                emergencyProtocolDtos,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Emergency protocols retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting emergency protocols");
            return PagedResult<List<EmergencyProtocolDto>>.Failure(
                $"An error occurred while retrieving emergency protocols: {ex.Message}");
        }
    }
}