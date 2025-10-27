using MediatR;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;

public class GetPendingApprovalsQuery : IRequest<PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; } = RiceProduction.Domain.Enums.TaskStatus.PendingApproval;

    public RiceProduction.Domain.Enums.TaskPriority? Priority { get; set; }
}

public class GetPendingApprovalsQueryValidator : AbstractValidator<GetPendingApprovalsQuery>
{
    public GetPendingApprovalsQueryValidator()
    {
        RuleFor(x => x.CurrentPage).GreaterThanOrEqualTo(1).WithMessage("CurrentPage must be 1 or greater.");
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).WithMessage("PageSize must be 1 or greater.");
    }
}

