using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.WeatherProtocolResponses;

namespace RiceProduction.Application.WeatherProtocolFeature.Queries.GetAllWeatherProtocols;

public class GetAllWeatherProtocolsQueryHandler : IRequestHandler<GetAllWeatherProtocolsQuery, PagedResult<List<WeatherProtocolResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllWeatherProtocolsQueryHandler> _logger;

    public GetAllWeatherProtocolsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllWeatherProtocolsQueryHandler> logger)
    {
        _context = context;
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

            var query = _context.WeatherProtocols.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.SearchName))
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                query = query.Where(w => w.Name.ToLower().Contains(searchTerm));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(w => w.IsActive == request.IsActive.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedWeatherProtocols = await query
                .OrderByDescending(w => w.CreatedAt)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var weatherProtocolResponses = pagedWeatherProtocols
                .Select(w => new WeatherProtocolResponse
                {
                    Id = w.Id,
                    Name = w.Name,
                    Description = w.Description,
                    Source = w.Source,
                    SourceLink = w.SourceLink,
                    ImageLinks = w.ImageLinks,
                    IsActive = w.IsActive,
                    Notes = w.Notes,
                    CreatedAt = w.CreatedAt,
                    LastModified = w.LastModified
                })
                .ToList();

            if (weatherProtocolResponses.Count == 0)
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