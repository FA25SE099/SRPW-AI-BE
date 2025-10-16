using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;

public class GetPendingApprovalsQuery : IRequest<Result<List<ExpertPendingPlanItemResponse>>>
{
    // Status filter (chỉ dùng để xác nhận, nhưng Status cứng là Submitted trong Handler)
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; } = RiceProduction.Domain.Enums.TaskStatus.PendingApproval;

    public TaskPriority? Priority { get; set; }
}
