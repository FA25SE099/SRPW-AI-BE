using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetAll
{
    public class GetAllFarmerQueriesHandler : IRequestHandler<GetAllFarmerQueries, PagedResult<IEnumerable<FarmerDTO>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAllFarmerQueriesHandler> _logger;

        public GetAllFarmerQueriesHandler(IApplicationDbContext context, ILogger<GetAllFarmerQueriesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResult<IEnumerable<FarmerDTO>>> Handle(GetAllFarmerQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting all Page: {PageNumber}, PageSize:{PageSize}",
                    request.PageNumber, request.PageSize);

                Guid? clusterId = null;
                var clusterManagerId = request.ClusterManagerId;
                if (clusterManagerId.HasValue)
                {
                    clusterId = await _context.ClusterManagers
                        .AsNoTracking()
                        .Where(cm => cm.Id == clusterManagerId.Value)
                        .Select(cm => cm.ClusterId)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var query = _context.Farmers
                    .AsNoTracking()
                    .Where(f => f.IsActive && f.ClusterId == clusterId);

                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var searchTerm = request.SearchTerm;
                    query = query.Where(f =>
                        (f.FullName != null && f.FullName.Contains(searchTerm)) ||
                        (f.PhoneNumber != null && f.PhoneNumber.Contains(searchTerm)));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var farmerDTOs = await query
                    .OrderBy(f => f.FullName)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(f => new FarmerDTO
                    {
                        FarmerId = f.Id,
                        FullName = f.FullName,
                        Address = f.Address,
                        PhoneNumber = f.PhoneNumber,
                        IsActive = f.IsActive,
                        IsVerified = f.IsVerified,
                        LastActivityAt = f.LastActivityAt,
                        FarmCode = f.FarmCode,
                        PlotCount = f.OwnedPlots.Count
                    })
                    .ToListAsync(cancellationToken);

                return PagedResult<IEnumerable<FarmerDTO>>.Success
                    (
                    data: farmerDTOs,
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: totalCount,
                    message: "Farmers retrieved successfully"
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured while getting all farmers");
                return PagedResult<IEnumerable<FarmerDTO>>.Failure(
                    error: ex.Message,
                    message: "Failed to retrieve farmers"
                    );

            }

        }
    }
}
