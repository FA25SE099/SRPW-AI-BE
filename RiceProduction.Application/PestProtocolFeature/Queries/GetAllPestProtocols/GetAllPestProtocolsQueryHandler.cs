using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PestProtocolResponses;

namespace RiceProduction.Application.PestProtocolFeature.Queries.GetAllPestProtocols;

public class GetAllPestProtocolsQueryHandler : IRequestHandler<GetAllPestProtocolsQuery, PagedResult<List<PestProtocolResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllPestProtocolsQueryHandler> _logger;

    public GetAllPestProtocolsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllPestProtocolsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<PestProtocolResponse>>> Handle(
        GetAllPestProtocolsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting pest protocols - Page: {Page}, PageSize: {PageSize}, Search: {Search}, IsActive: {IsActive}",
                request.CurrentPage, request.PageSize, request.SearchName, request.IsActive);

            var query = _context.PestProtocols.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.SearchName))
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == request.IsActive.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedPestProtocols = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var pestProtocolResponses = pagedPestProtocols
                .Select(p => new PestProtocolResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Type = p.Type,
                    ImageLinks = p.ImageLinks,
                    IsActive = p.IsActive,
                    Notes = p.Notes,
                    CreatedAt = p.CreatedAt,
                    LastModified = p.LastModified
                })
                .ToList();

            if (pestProtocolResponses.Count == 0)
            {
                return PagedResult<List<PestProtocolResponse>>.Success(
                    new List<PestProtocolResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No pest protocols found matching the criteria.");
            }

            _logger.LogInformation(
                "Retrieved {Count} pest protocols out of {Total}",
                pestProtocolResponses.Count, totalCount);

            return PagedResult<List<PestProtocolResponse>>.Success(
                pestProtocolResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Pest protocols retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting pest protocols");
            return PagedResult<List<PestProtocolResponse>>.Failure(
                $"An error occurred while retrieving pest protocols: {ex.Message}");
        }
    }
}