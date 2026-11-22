using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationFeature.Event;
using RiceProduction.Domain.Entities;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;

public class ApproveRejectPlanCommandHandler :
    IRequestHandler<ApproveRejectPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveRejectPlanCommandHandler> _logger;
    private readonly IMediator _mediator;
    public ApproveRejectPlanCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ApproveRejectPlanCommandHandler> logger,
        IMediator mediator
        ) 
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<Guid>> Handle(ApproveRejectPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(p => p.Id == request.PlanId);

            var expertId = request.ExpertId;

            if (expertId == null)
            {
                return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
            }

            if (plan == null)
            {
                return Result<Guid>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            if (!plan.CanBeReviewed)
            {
                return Result<Guid>.Failure($"Plan is currently in status '{plan.Status}'. Only PendingApproval plans can be approved or rejected.", "InvalidStatus");
            }

            if (request.Approved)
            {
                plan.Approve(expertId.Value);

                _logger.LogInformation("Plan {PlanId} approved by Expert {ExpertId}.", plan.Id, expertId);
                // Note: CultivationVersions are now created per PlotCultivation by ProductionPlanApprovalEventHandler
            }
            else
            {
                plan.Reject(expertId.Value);

                _logger.LogInformation("Plan {PlanId} rejected by Expert {ExpertId}. Reason: {Notes}", plan.Id, expertId, request.Notes);
            }

            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();

            string action = request.Approved ? "Approved" : "Rejected";
            if (request.Approved)
            {
                 await _mediator.Publish(new ProductionPlanApprovalEvent()
                {
                    PlanId = plan.Id
                }, cancellationToken);
            }
            
            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' successfully {action}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving/rejecting production plan: {PlanId}", request.PlanId);

            return Result<Guid>.Failure("An error occurred during approval/rejection process.", "ApprovalRejectedFailed");
        }
    }
}