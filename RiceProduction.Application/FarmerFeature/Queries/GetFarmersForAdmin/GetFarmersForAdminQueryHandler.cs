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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetFarmersForAdminQueryHandler> _logger;

    public GetFarmersForAdminQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetFarmersForAdminQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
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

            // Get ALL farmers (no IsActive filter for admin - they should see everything)
            var farmers = await _unitOfWork.FarmerRepository.ListAsync(
                filter: f => true, // Get ALL farmers including inactive ones
                includeProperties: q => q
                    .Include(f => f.Cluster)
                    .Include(f => f.OwnedPlots)
            );

            var farmersList = farmers.ToList();

            _logger.LogInformation("Retrieved {Count} total farmers before filtering", farmersList.Count);

            // Apply optional filters only if specified
            if (request.ClusterId.HasValue)
            {
                farmersList = farmersList.Where(f => f.ClusterId == request.ClusterId.Value).ToList();
                _logger.LogInformation("After cluster filter: {Count} farmers", farmersList.Count);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchTerm = request.Search.ToLower().Trim();
                farmersList = farmersList.Where(f =>
                    (f.FullName != null && f.FullName.ToLower().Contains(searchTerm)) ||
                    (f.Email != null && f.Email.ToLower().Contains(searchTerm)) ||
                    (f.FarmCode != null && f.FarmCode.ToLower().Contains(searchTerm))
                ).ToList();
                _logger.LogInformation("After search filter: {Count} farmers", farmersList.Count);
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneNumber = request.PhoneNumber.Trim();
                farmersList = farmersList.Where(f => f.PhoneNumber != null && f.PhoneNumber.Contains(phoneNumber)).ToList();
                _logger.LogInformation("After phone filter: {Count} farmers", farmersList.Count);
            }

            if (!string.IsNullOrWhiteSpace(request.FarmerStatus))
            {
                if (Enum.TryParse<FarmerStatus>(request.FarmerStatus, true, out var statusEnum))
                {
                    farmersList = farmersList.Where(f => f.Status == statusEnum).ToList();
                    _logger.LogInformation("After status filter: {Count} farmers", farmersList.Count);
                }
            }

            // Order by last activity (newest first), then by creation date
            farmersList = farmersList
                .OrderByDescending(f => f.LastActivityAt ?? DateTime.MinValue)
                .ThenByDescending(f => f.Id)
                .ToList();

            // Get total count after filtering
            var totalCount = farmersList.Count;

            // Apply pagination
            var pagedFarmers = farmersList
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to response
            var response = pagedFarmers.Select(f => new FarmerListResponse
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
                ClusterName = f.Cluster?.ClusterName,
                PlotCount = f.OwnedPlots?.Count ?? 0
            }).ToList();

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
