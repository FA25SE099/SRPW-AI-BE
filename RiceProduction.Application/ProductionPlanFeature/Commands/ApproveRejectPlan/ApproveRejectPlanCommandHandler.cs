using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationFeature.Event;
using RiceProduction.Application.SmsFeature.Event;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
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

            if (plan.Status != RiceProduction.Domain.Enums.TaskStatus.PendingApproval)
            {
                return Result<Guid>.Failure($"Plan is currently in status '{plan.Status}'. Only PendingApproval plans can be approved or rejected.", "InvalidStatus");
            }

            if (request.Approved)
            {
                plan.Status = RiceProduction.Domain.Enums.TaskStatus.Approved;
                plan.ApprovedAt = DateTime.UtcNow;
                plan.ApprovedBy = expertId; // <-- Gán ID chuyên gia

                // Create default cultivation version "0"
                var cultivationVersionRepo = _unitOfWork.Repository<CultivationVersion>();
                var newCultivationVersionId = await cultivationVersionRepo.GenerateNewGuid(Guid.NewGuid());
                var cultivationVersion = new CultivationVersion
                {
                    Id = newCultivationVersionId,
                    ProductionPlanId = plan.Id,
                    VersionName = "0",
                    VersionOrder = 1,
                    IsActive = true,
                    Reason = "Initial version created upon plan approval",
                    ActivatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<CultivationVersion>().AddAsync(cultivationVersion);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created default CultivationVersion '{VersionName}' with ID {VersionId} for Plan {PlanId}",
                    cultivationVersion.VersionName, cultivationVersion.Id, plan.Id);

                // Attach version ID to all cultivation tasks
                var cultivationTasks = await _unitOfWork.Repository<CultivationTask>()
                    .GetQueryable()
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionStage)
                    .Where(ct => ct.ProductionPlanTask.ProductionStage.ProductionPlanId == plan.Id)
                    .ToListAsync(cancellationToken);

                if (cultivationTasks.Any())
                {
                    foreach (var cultivationTask in cultivationTasks)
                    {
                        cultivationTask.VersionId = cultivationVersion.Id;
                    }

                    _unitOfWork.Repository<CultivationTask>().UpdateRange(cultivationTasks);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Attached VersionId {VersionId} to {TaskCount} cultivation tasks for Plan {PlanId}",
                        cultivationVersion.Id, cultivationTasks.Count, plan.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "No cultivation tasks found for Plan {PlanId} to attach version",
                        plan.Id);
                }

                _logger.LogInformation("Plan {PlanId} approved by Expert {ExpertId}.", plan.Id, expertId);
            }
            else
            {
                plan.Status = RiceProduction.Domain.Enums.TaskStatus.Cancelled;
                
                _logger.LogInformation("Plan {PlanId} rejected by Expert {ExpertId}. Reason: {Notes}", plan.Id, expertId, request.Notes);
            }

            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId; // Cập nhật người chỉnh sửa lần cuối là chuyên gia

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