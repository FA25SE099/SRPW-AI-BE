using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApprovedPlan;

public class GetApprovedPlansQueryHandler :
    IRequestHandler<GetApprovedPlansQuery, PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetApprovedPlansQueryHandler> _logger;

    public GetApprovedPlansQueryHandler(IApplicationDbContext context, ILogger<GetApprovedPlansQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<ExpertPendingPlanItemResponse>>> Handle(GetApprovedPlansQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _context.ProductionPlans
                .AsNoTracking()
                .Where(p => p.Status == TaskStatus.Approved);

            if (request.Year.HasValue)
            {
                query = query.Where(p => p.BasePlantingDate.Year == request.Year.Value);
            }

            if (request.SeasonId.HasValue)
            {
                query = query.Where(p =>
                    p.GroupId.HasValue &&
                    p.Group != null &&
                    p.Group.YearSeason != null &&
                    p.Group.YearSeason.SeasonId == request.SeasonId.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedPlans = await query
                .Include(p => p.Submitter)
                .OrderByDescending(p => p.ApprovedAt)
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
                "Successfully retrieved approved plans.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approved plans.");
            return PagedResult<List<ExpertPendingPlanItemResponse>>.Failure("Failed to retrieve approved plans.", "GetApprovedPlansFailed");
        }
    }
}