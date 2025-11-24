using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PestProtocolResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Queries.GetAllPestProtocols;

public class GetAllPestProtocolsQueryHandler : IRequestHandler<GetAllPestProtocolsQuery, PagedResult<List<PestProtocolResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAllPestProtocolsQueryHandler> _logger;

    public GetAllPestProtocolsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAllPestProtocolsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
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

            // Build filter expression
            Expression<Func<PestProtocol, bool>>? filter = null;

            if (!string.IsNullOrWhiteSpace(request.SearchName) && request.IsActive.HasValue)
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                filter = p => p.Name.ToLower().Contains(searchTerm) && p.IsActive == request.IsActive.Value;
            }
            else if (!string.IsNullOrWhiteSpace(request.SearchName))
            {
                var searchTerm = request.SearchName.ToLower().Trim();
                filter = p => p.Name.ToLower().Contains(searchTerm);
            }
            else if (request.IsActive.HasValue)
            {
                filter = p => p.IsActive == request.IsActive.Value;
            }

            // Get pest protocols with filter and ordering
            var pestProtocols = await _unitOfWork.Repository<PestProtocol>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(p => p.CreatedAt));

            // Get total count
            var totalCount = pestProtocols.Count();

            // Apply pagination
            var pagedPestProtocols = pestProtocols
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Manual mapping
            var pestProtocolResponses = pagedPestProtocols
                .Select(p => new PestProtocolResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Type = p.Type,
                    ImageLink = p.ImageLink,
                    IsActive = p.IsActive,
                    Notes = p.Notes,
                    CreatedAt = p.CreatedAt,
                    LastModified = p.LastModified
                })
                .ToList();

            if (!pestProtocolResponses.Any())
            {
                return PagedResult<List<PestProtocolResponse>>.Failure(
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