using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateFarmersInCluster;

public class GetLateFarmersInClusterQueryHandler : IRequestHandler<GetLateFarmersInClusterQuery, PagedResult<IEnumerable<FarmerWithLateCountDTO>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetLateFarmersInClusterQueryHandler> _logger;

    public GetLateFarmersInClusterQueryHandler(IApplicationDbContext context, ILogger<GetLateFarmersInClusterQueryHandler> logger)
    {
        _context = context;
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
                var expert = await _context.AgronomyExperts
                    .AsNoTracking()
                    .Where(e => e.Id == request.AgronomyExpertId.Value)
                    .Select(e => new { e.Id, e.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (expert == null)
                {
                    return PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("Agronomy Expert not found");
                }
                clusterId = expert.ClusterId;
            }
            else if (request.SupervisorId.HasValue)
            {
                var supervisor = await _context.Supervisors
                    .AsNoTracking()
                    .Where(s => s.Id == request.SupervisorId.Value)
                    .Select(s => new { s.Id, s.ClusterId })
                    .FirstOrDefaultAsync(cancellationToken);
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

            // Get distinct farmer IDs who have late records from active versions in the cluster
            var farmerIdsWithLateRecords = await _context.LateFarmerRecords
                .AsNoTracking()
                .Where(lr => lr.CultivationTask.PlotCultivation.Plot.GroupPlots
                    .Any(gp => gp.Group.ClusterId == clusterId.Value)
                    && lr.CultivationTask.Version != null
                    && lr.CultivationTask.Version.IsActive)
                .Select(lr => lr.FarmerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (farmerIdsWithLateRecords.Count == 0)
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
            var farmerQuery = _context.Farmers
                .AsNoTracking()
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

            // Get paged farmers without loading PostGIS plot geometry columns
            var farmers = await farmerQuery
                .OrderBy(f => f.FullName)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(f => new
                {
                    f.Id,
                    f.FullName,
                    f.Address,
                    f.PhoneNumber,
                    f.IsActive,
                    f.IsVerified,
                    f.LastActivityAt,
                    f.FarmCode,
                    PlotCount = f.OwnedPlots.Count
                })
                .ToListAsync(cancellationToken);

            // Batch fetch late counts for the paged farmers
            var pagedFarmerIds = farmers.Select(f => f.Id).ToList();
            var lateCounts = await _context.LateFarmerRecords
                .AsNoTracking()
                .Where(l => pagedFarmerIds.Contains(l.FarmerId)
                    && l.CultivationTask.Version != null
                    && l.CultivationTask.Version.IsActive)
                .GroupBy(l => l.FarmerId)
                .Select(g => new { FarmerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FarmerId, x => x.Count, cancellationToken);

            var result = farmers.Select(farmer => new FarmerWithLateCountDTO
            {
                FarmerId = farmer.Id,
                FullName = farmer.FullName,
                Address = farmer.Address,
                PhoneNumber = farmer.PhoneNumber,
                IsActive = farmer.IsActive,
                IsVerified = farmer.IsVerified,
                LastActivityAt = farmer.LastActivityAt,
                FarmCode = farmer.FarmCode,
                PlotCount = farmer.PlotCount,
                LateCount = lateCounts.GetValueOrDefault(farmer.Id, 0)
            }).ToList();

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
