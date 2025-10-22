using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;

public class GetPendingApprovalsQueryHandler :
    IRequestHandler<GetPendingApprovalsQuery, Result<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPendingApprovalsQueryHandler> _logger;

    public GetPendingApprovalsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPendingApprovalsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<ExpertPendingPlanItemResponse>>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var planRepo = _unitOfWork.Repository<ProductionPlan>();

            // Query plans with Status = Submitted (chờ phê duyệt)
            var plans = await planRepo.ListAsync(
                filter: p => p.Status == RiceProduction.Domain.Enums.TaskStatus.PendingApproval,
                orderBy: q => q.OrderBy(p => p.SubmittedAt),
                includeProperties: q => q
                    .Include(p => p.Group)
                    .Include(p => p.Submitter)
            );

            var response = plans.Select(p => new ExpertPendingPlanItemResponse
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

            return Result<List<ExpertPendingPlanItemResponse>>.Success(response, "Successfully retrieved pending approval plans.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approval plans.");
            return Result<List<ExpertPendingPlanItemResponse>>.Failure("Failed to retrieve pending plans.", "GetPendingApprovalsFailed");
        }
    }
}
