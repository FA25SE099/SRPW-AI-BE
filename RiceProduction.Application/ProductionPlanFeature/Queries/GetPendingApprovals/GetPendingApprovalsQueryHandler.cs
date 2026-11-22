using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;

public class GetPendingApprovalsQueryHandler :
    IRequestHandler<GetPendingApprovalsQuery, PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetPendingApprovalsQueryHandler> _logger;

    public GetPendingApprovalsQueryHandler(IApplicationDbContext context, ILogger<GetPendingApprovalsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<ExpertPendingPlanItemResponse>>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.ProductionPlans
                .AsNoTracking()
                .Where(p => p.Status == TaskStatus.PendingApproval);

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedPlans = await query
                .Include(p => p.Submitter)
                .OrderBy(p => p.SubmittedAt)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var responseData = pagedPlans.Select(p => new ExpertPendingPlanItemResponse
            {
                Id = p.Id,
                PlanName = p.PlanName,
                GroupId = p.GroupId,
                GroupArea = p.TotalArea.HasValue ? $"{p.TotalArea.Value} ha" : "N/A",
                BasePlantingDate = p.BasePlantingDate,
                Status = p.Status,
                SubmittedAt = p.SubmittedAt,
                SubmitterName = p.Submitter != null ? p.Submitter.FullName : "Unknown"
            }).ToList();

            return PagedResult<List<ExpertPendingPlanItemResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved pending approval plans.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approval plans.");
            return PagedResult<List<ExpertPendingPlanItemResponse>>.Failure("Failed to retrieve pending plans.", "GetPendingApprovalsFailed");
        }
    }
}
