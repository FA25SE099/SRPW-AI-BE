using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetEmergencyPlan;

public class GetEmergencyPlansQueryHandler : IRequestHandler<GetEmergencyPlansQuery, PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetEmergencyPlansQueryHandler> _logger;

    public GetEmergencyPlansQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetEmergencyPlansQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
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

            // Build the query
            var query = _unitOfWork.Repository<ProductionPlan>()
                .GetQueryable()
                .Where(p => p.Status == RiceProduction.Domain.Enums.TaskStatus.Emergency)
                .Include(p => p.Group)
                    .ThenInclude(g => g.Cluster)
                .Include(p => p.Submitter)
                .AsQueryable();

            // Apply GroupId filter
            if (request.GroupId.HasValue)
            {
                query = query.Where(p => p.GroupId == request.GroupId.Value);
            }

            // Apply ClusterId filter
            if (request.ClusterId.HasValue)
            {
                query = query.Where(p => p.Group != null && p.Group.ClusterId == request.ClusterId.Value);
            }

            // Apply search term filter
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower().Trim();
                query = query.Where(p => p.PlanName.ToLower().Contains(searchTerm));
            }

            // Apply ordering (most recent emergency first)
            query = query.OrderByDescending(p => p.CreatedAt);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var pagedPlans = await query
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to response
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
