using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetEmergencyPlan;

public class GetEmergencyPlansQueryHandler : IRequestHandler<GetEmergencyPlansQuery, PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetEmergencyPlansQueryHandler> _logger;

    public GetEmergencyPlansQueryHandler(
        IApplicationDbContext context,
        ILogger<GetEmergencyPlansQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<ExpertPendingPlanItemResponse>>> Handle(
        GetEmergencyPlansQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting emergency plans - Page: {Page}, PageSize: {PageSize}, GroupId: {GroupId}, ClusterId: {ClusterId}, Search: {Search}",
                request.CurrentPage, request.PageSize, request.GroupId, request.ClusterId, request.SearchTerm);

            var query = _context.ProductionPlans
                .AsNoTracking()
                .Where(p => p.Status == TaskStatus.Emergency);

            if (request.GroupId.HasValue)
            {
                query = query.Where(p => p.GroupId == request.GroupId.Value);
            }

            if (request.ClusterId.HasValue)
            {
                query = query.Where(p => p.Group != null && p.Group.ClusterId == request.ClusterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower().Trim();
                query = query.Where(p => p.PlanName.ToLower().Contains(searchTerm));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedPlans = await query
                .Include(p => p.Submitter)
                .OrderByDescending(p => p.CreatedAt)
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

            _logger.LogInformation(
                "Retrieved {Count} emergency plans out of {Total}",
                responseData.Count, totalCount);

            return PagedResult<List<ExpertPendingPlanItemResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved emergency plans.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emergency plans.");
            return PagedResult<List<ExpertPendingPlanItemResponse>>.Failure(
                "Failed to retrieve emergency plans.",
                "GetEmergencyPlansFailed");
        }
    }
}
