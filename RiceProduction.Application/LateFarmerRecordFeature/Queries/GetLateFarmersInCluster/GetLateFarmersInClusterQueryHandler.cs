using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateFarmersInCluster;

public class GetLateFarmersInClusterQueryHandler : IRequestHandler<GetLateFarmersInClusterQuery, PagedResult<IEnumerable<FarmerWithLateCountDTO>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetLateFarmersInClusterQueryHandler> _logger;

    public GetLateFarmersInClusterQueryHandler(IUnitOfWork unitOfWork, ILogger<GetLateFarmersInClusterQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<IEnumerable<FarmerWithLateCountDTO>>> Handle(GetLateFarmersInClusterQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting farmers with late records in cluster");

            Guid? clusterId = null;

            // Get cluster ID from AgronomyExpert or Supervisor
            if (request.AgronomyExpertId.HasValue)
            {
                var expert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(request.AgronomyExpertId.Value, cancellationToken);
                if (expert == null)
                {
                    return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("Agronomy Expert not found");
                }
                clusterId = expert.ClusterId;
            }
            else if (request.SupervisorId.HasValue)
            {
                var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(request.SupervisorId.Value, cancellationToken);
                if (supervisor == null)
                {
                    return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("Supervisor not found");
                }
                clusterId = supervisor.ClusterId;
            }

            if (!clusterId.HasValue)
            {
                return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("Cluster ID not found for the specified user");
            }

            // Get all late farmer records in the cluster
            var lateRecordsQuery = _unitOfWork.LateFarmerRecordRepository.GetQueryable()
                .Where(lr => lr.ClusterId == clusterId.Value);

            // Get distinct farmer IDs who have late records
            var farmerIdsWithLateRecords = await lateRecordsQuery
                .Select(lr => lr.FarmerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!farmerIdsWithLateRecords.Any())
            {
                return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Success(
                    data: new List<FarmerWithLateCountDTO>(),
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: 0,
                    message: "No farmers with late records found in the cluster"
                );
            }

            // Build farmer query
            var farmerQuery = _unitOfWork.FarmerRepository.GetQueryable()
                .Include(f => f.OwnedPlots)
                .Where(f => farmerIdsWithLateRecords.Contains(f.Id) && f.IsActive);

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                farmerQuery = farmerQuery.Where(f => 
                    (f.FullName != null && f.FullName.Contains(request.SearchTerm)) || 
                    (f.PhoneNumber != null && f.PhoneNumber.Contains(request.SearchTerm)));
            }

            // Get total count
            var totalCount = await farmerQuery.CountAsync(cancellationToken);

            // Get paged farmers
            var farmers = await farmerQuery
                .OrderBy(f => f.FullName)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Get late counts for each farmer
            var result = new List<FarmerWithLateCountDTO>();
            foreach (var farmer in farmers)
            {
                var lateCount = await _unitOfWork.LateFarmerRecordRepository.GetLateCountByFarmerIdAsync(farmer.Id, cancellationToken);
                result.Add(new FarmerWithLateCountDTO
                {
                    FarmerId = farmer.Id,
                    FullName = farmer.FullName,
                    Address = farmer.Address,
                    PhoneNumber = farmer.PhoneNumber,
                    IsActive = farmer.IsActive,
                    IsVerified = farmer.IsVerified,
                    LastActivityAt = farmer.LastActivityAt,
                    FarmCode = farmer.FarmCode,
                    PlotCount = farmer.OwnedPlots?.Count ?? 0,
                    LateCount = lateCount
                });
            }

            return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Success(
                data: result,
                currentPage: request.PageNumber,
                pageSize: request.PageSize,
                totalCount: totalCount,
                message: "Farmers with late records retrieved successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting farmers with late records in cluster");
            return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("An error occurred while processing your request");
        }
    }
}
