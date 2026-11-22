using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.FarmerResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmersForAdmin;

public class GetFarmersForAdminQueryHandler : IRequestHandler<GetFarmersForAdminQuery, PagedResult<List<FarmerListResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmersForAdminQueryHandler> _logger;

    public GetFarmersForAdminQueryHandler(
        IApplicationDbContext context,
        ILogger<GetFarmersForAdminQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmerListResponse>>> Handle(
        GetFarmersForAdminQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting ALL farmers for admin - Page: {Page}, Size: {Size}, Search: {Search}",
                request.CurrentPage, request.PageSize, request.Search);

            // Admin sees ALL farmers (no IsActive filter); optional filters are applied in the database
            var query = _context.Farmers.AsNoTracking();

            if (request.ClusterId.HasValue)
            {
                query = query.Where(f => f.ClusterId == request.ClusterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchTerm = request.Search.ToLower().Trim();
                query = query.Where(f =>
                    (f.FullName != null && f.FullName.ToLower().Contains(searchTerm)) ||
                    (f.Email != null && f.Email.ToLower().Contains(searchTerm)) ||
                    (f.FarmCode != null && f.FarmCode.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneNumber = request.PhoneNumber.Trim();
                query = query.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.FarmerStatus) &&
                Enum.TryParse<FarmerStatus>(request.FarmerStatus, true, out var statusEnum))
            {
                query = query.Where(f => f.Status == statusEnum);
            }

            // Total count after filtering
            var totalCount = await query.CountAsync(cancellationToken);

            // Order by last activity (newest first), then by id
            var response = await query
                .OrderByDescending(f => f.LastActivityAt ?? DateTime.MinValue)
                .ThenByDescending(f => f.Id)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(f => new FarmerListResponse
                {
                    FarmerId = f.Id,
                    FullName = f.FullName,
                    Email = f.Email,
                    Address = f.Address,
                    PhoneNumber = f.PhoneNumber,
                    FarmCode = f.FarmCode,
                    IsActive = f.IsActive,
                    IsVerified = f.EmailConfirmed,
                    FarmerStatus = f.Status.ToString(),
                    LastActivityAt = f.LastActivityAt,
                    ClusterId = f.ClusterId,
                    ClusterName = f.Cluster != null ? f.Cluster.ClusterName : null,
                    PlotCount = f.OwnedPlots.Count
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} farmers out of {Total} total (Page {Page}/{TotalPages})",
                response.Count, totalCount, request.CurrentPage, (int)Math.Ceiling(totalCount / (double)request.PageSize));

            return PagedResult<List<FarmerListResponse>>.Success(
                response,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved all farmers. Total: {totalCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting all farmers for admin");
            return PagedResult<List<FarmerListResponse>>.Failure(
                "An error occurred while retrieving farmers.",
                "GetFarmersFailed");
        }
    }
}
