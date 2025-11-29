using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.WeatherProtocolResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.WeatherProtocolFeature.Queries.GetAllWeatherProtocols;

public class GetAllWeatherProtocolsQueryHandler : IRequestHandler<GetAllWeatherProtocolsQuery, PagedResult<List<WeatherProtocolResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAllWeatherProtocolsQueryHandler> _logger;

    public GetAllWeatherProtocolsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAllWeatherProtocolsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<WeatherProtocolResponse>>> Handle(
        GetAllWeatherProtocolsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting weather protocols - Page: {Page}, PageSize: {PageSize}, Search: {Search}, IsActive: {IsActive}",
                request.CurrentPage, request.PageSize, request.SearchName, request.IsActive);

            // Build filter expression
            Expression<Func<WeatherProtocol, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(request.SearchName) && request.IsActive.HasValue)
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                filter = w => w.Name.ToLower().Contains(searchTerm) && w.IsActive == request.IsActive.Value;
            }
            else if (!string.IsNullOrWhiteSpace(request.SearchName))
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                filter = w => w.Name.ToLower().Contains(searchTerm);
            }
            else if (request.IsActive.HasValue)
            {
                filter = w => w.IsActive == request.IsActive.Value;
            }

            // Get weather protocols with filter and ordering
            var weatherProtocols = await _unitOfWork.Repository<WeatherProtocol>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(w => w.CreatedAt));

            // Get total count
            var totalCount = weatherProtocols.Count();

            // Apply pagination
            var pagedWeatherProtocols = weatherProtocols
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Manual mapping
            var weatherProtocolResponses = pagedWeatherProtocols
                .Select(w => new WeatherProtocolResponse
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    Source = w.Source,
                    SourceLink = w.SourceLink,
                    ImageLinks = w.ImageLinks, // ✅ Now returns list
                    IsActive = w.IsActive,
                    Notes = w.Notes,
                    CreatedAt = w.CreatedAt,
                    LastModified = w.LastModified
                })
                .ToList();

            if (!weatherProtocolResponses.Any())
            {
                return PagedResult<List<WeatherProtocolResponse>>.Success(
                    new List<WeatherProtocolResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No weather protocols found matching the criteria.");
            }

            _logger.LogInformation(
                "Retrieved {Count} weather protocols out of {Total}",
                weatherProtocolResponses.Count, totalCount);

            return PagedResult<List<WeatherProtocolResponse>>.Success(
                weatherProtocolResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Weather protocols retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting weather protocols");
            return PagedResult<List<WeatherProtocolResponse>>.Failure(
                $"An error occurred while retrieving weather protocols: {ex.Message}");
        }
    }
}