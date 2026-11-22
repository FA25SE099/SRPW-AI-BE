using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetFarmersBySupervisor;

public class GetFarmersBySupervisorQueryHandler : IRequestHandler<GetFarmersBySupervisorQuery, PagedResult<List<FarmerDTO>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetFarmersBySupervisorQueryHandler> _logger;

    public GetFarmersBySupervisorQueryHandler(
        IApplicationDbContext context,
        ILogger<GetFarmersBySupervisorQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmerDTO>>> Handle(
        GetFarmersBySupervisorQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var supervisor = await _context.Supervisors
                .AsNoTracking()
                .Where(s => s.Id == request.SupervisorId)
                .Select(s => new { s.Id, s.ClusterId })
                .FirstOrDefaultAsync(cancellationToken);

            if (supervisor == null)
            {
                return PagedResult<List<FarmerDTO>>.Failure(
                    "Supervisor not found.",
                    "NotFound");
            }

            IQueryable<Farmer> farmersQuery = _context.Farmers
                .AsNoTracking()
                .Where(f => f.IsActive);

            if (request.OnlyAssigned)
            {
                // Supervisor -> Groups -> GroupPlots -> Plots -> FarmerId
                var farmerIds = await _context.GroupPlots
                    .AsNoTracking()
                    .Where(gp => gp.Group.SupervisorId == request.SupervisorId)
                    .Select(gp => gp.Plot.FarmerId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                if (!farmerIds.Any())
                {
                    return PagedResult<List<FarmerDTO>>.Success(
                        new List<FarmerDTO>(),
                        request.CurrentPage,
                        request.PageSize,
                        0,
                        "No farmers found in supervised groups.");
                }

                farmersQuery = farmersQuery.Where(f => farmerIds.Contains(f.Id));
            }
            else
            {
                if (!supervisor.ClusterId.HasValue)
                {
                    return PagedResult<List<FarmerDTO>>.Success(
                        new List<FarmerDTO>(),
                        request.CurrentPage,
                        request.PageSize,
                        0,
                        "Supervisor is not assigned to any cluster.");
                }

                var clusterId = supervisor.ClusterId.Value;
                farmersQuery = farmersQuery.Where(f => f.ClusterId == clusterId);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.Trim();
                var searchLower = search.ToLower();
                farmersQuery = farmersQuery.Where(f =>
                    (f.FullName != null && f.FullName.ToLower().Contains(searchLower)) ||
                    (f.PhoneNumber != null && f.PhoneNumber.Contains(search)));
            }

            var totalCount = await farmersQuery.CountAsync(cancellationToken);

            var farmerDTOs = await farmersQuery
                .OrderBy(f => f.FullName)
                .ThenBy(f => f.Id)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(f => new FarmerDTO
                {
                    FarmerId = f.Id,
                    FullName = f.FullName,
                    Address = f.Address,
                    PhoneNumber = f.PhoneNumber,
                    IsActive = f.IsActive,
                    IsVerified = f.EmailConfirmed,
                    LastActivityAt = f.LastActivityAt,
                    FarmCode = f.FarmCode,
                    PlotCount = f.OwnedPlots.Count
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} farmers for supervisor {SupervisorId} (OnlyAssigned: {OnlyAssigned})",
                farmerDTOs.Count,
                request.SupervisorId,
                request.OnlyAssigned);

            return PagedResult<List<FarmerDTO>>.Success(
                farmerDTOs,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {(request.OnlyAssigned ? "assigned" : "all")} farmers.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving farmers for supervisor {SupervisorId}",
                request.SupervisorId);
            return PagedResult<List<FarmerDTO>>.Failure(
                "An error occurred while retrieving farmers.",
                "GetFarmersFailed");
        }
    }
}
