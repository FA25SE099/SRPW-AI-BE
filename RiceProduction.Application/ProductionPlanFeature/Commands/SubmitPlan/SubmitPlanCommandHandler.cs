using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Commands.SubmitPlan;

public class SubmitPlanCommandHandler : IRequestHandler<SubmitPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitPlanCommandHandler> _logger;

    public SubmitPlanCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<SubmitPlanCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(SubmitPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var supervisorId = request.SupervisorId;

            if (supervisorId == null)
            {
                return Result<Guid>.Failure("Current supervisor user ID not found.", "AuthenticationRequired");
            }

            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(p => p.Id == request.PlanId);

            if (plan == null)
            {
                return Result<Guid>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            if (plan.Status != TaskStatus.Draft)
            {
                return Result<Guid>.Failure(
                    $"Plan is currently in status '{plan.Status}'. Only Draft plans can be submitted.",
                    "InvalidStatus");
            }

            plan.Status = TaskStatus.PendingApproval;
            plan.SubmittedAt = DateTime.UtcNow;
            plan.SubmittedBy = supervisorId;
            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = supervisorId;

            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();

            _logger.LogInformation(
                "Plan {PlanId} submitted for approval by Supervisor {SupervisorId}.",
                plan.Id, supervisorId);

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' successfully submitted for approval.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting production plan: {PlanId}", request.PlanId);
            return Result<Guid>.Failure("An error occurred while submitting the plan.", "SubmitPlanFailed");
        }
    }
}

