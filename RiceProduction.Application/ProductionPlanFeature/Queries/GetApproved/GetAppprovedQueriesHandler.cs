using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApproved
{
    public class GetAppprovedQueriesHandler : IRequestHandler<GetApprovedQueries, Result<List<ProductionPlanDTO>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<GetAppprovedQueriesHandler> _logger;

        public GetAppprovedQueriesHandler(IApplicationDbContext context, ILogger<GetAppprovedQueriesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<ProductionPlanDTO>>> Handle(GetApprovedQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting to retrieve approved production plans with filters: GroupId={GroupId}, SupervisorId={SupervisorId}, FromDate={FromDate}, ToDate={ToDate}",
                     request.GroupId, request.SupervisorId, request.FromDate, request.ToDate);

                var query = _context.ProductionPlans
                    .AsNoTracking()
                    .Where(st => st.Status == RiceProduction.Domain.Enums.TaskStatus.Approved);

                if (request.GroupId.HasValue)
                {
                    var groupId = request.GroupId.Value;
                    query = query.Where(pp => pp.GroupId == groupId);
                }

                if (request.SupervisorId.HasValue)
                {
                    var supervisorId = request.SupervisorId.Value;
                    query = query.Where(pp => pp.SubmittedBy == supervisorId ||
                                              (pp.Group != null && pp.Group.SupervisorId == supervisorId));
                }

                if (request.FromDate.HasValue)
                {
                    var fromUtc = request.FromDate.Value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Utc)
                        : request.FromDate.Value.ToUniversalTime();
                    query = query.Where(pp => pp.ApprovedAt >= fromUtc);
                }

                if (request.ToDate.HasValue)
                {
                    var toUtc = request.ToDate.Value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Utc)
                        : request.ToDate.Value.ToUniversalTime();
                    query = query.Where(pp => pp.ApprovedAt <= toUtc);
                }

                var productionPlan = await query
                    .Include(g => g.Group)
                    .Include(su => su.Submitter)
                    .Include(ap => ap.Approver)
                    .Include(s => s.CurrentProductionStages).ThenInclude(t => t.ProductionPlanTasks)
                    .AsSplitQuery()
                    .OrderByDescending(pp => pp.ApprovedAt)
                    .ThenBy(pp => pp.BasePlantingDate)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Found {Count} approved production plans", productionPlan.Count);

                if (!productionPlan.Any())
                {
                    _logger.LogWarning("No approved production plans found with the specified filters");
                    return Result<List<ProductionPlanDTO>>.Success(
                        new List<ProductionPlanDTO>(),
                        "No approved production plans found");
                }

                var productionPlanDTOs = productionPlan.Select(pp => pp.ToDto()).ToList();
                _logger.LogInformation("Successfully mapped {Count} production plans to DTOs", productionPlanDTOs.Count);
                return Result<List<ProductionPlanDTO>>.Success(
                   productionPlanDTOs,
                   $"Successfully retrieved {productionPlanDTOs.Count} approved production plans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving approved production plans");
                return Result<List<ProductionPlanDTO>>.Failure(
                    $"Error retrieving approved production plans: {ex.Message}");
            }
        }
    }
}
