using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;

public class ApproveRejectPlanCommandHandler :
    IRequestHandler<ApproveRejectPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveRejectPlanCommandHandler> _logger;
    private readonly IUser _currentUser; // <-- Đã thêm IUser

    public ApproveRejectPlanCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ApproveRejectPlanCommandHandler> logger,
        IUser currentUser) // <-- Inject IUser
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUser = currentUser; // <-- Khởi tạo
    }

    public async Task<Result<Guid>> Handle(ApproveRejectPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(p => p.Id == request.PlanId);

            var expertId = _currentUser.Id;

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
                return Result<Guid>.Failure($"Plan is currently in status '{plan.Status}'. Only Submitted plans can be approved or rejected.", "InvalidStatus");
            }

            if (request.Approved)
            {
                plan.Status = RiceProduction.Domain.Enums.TaskStatus.Approved;
                plan.ApprovedAt = DateTime.UtcNow;
                plan.ApprovedBy = expertId; // <-- Gán ID chuyên gia
                _logger.LogInformation("Plan {PlanId} approved by Expert {ExpertId}.", plan.Id, expertId);
            }
            else
            {
                plan.Status = RiceProduction.Domain.Enums.TaskStatus.Cancelled;
                // Nếu bạn có trường RejectionNotes trong Plan entity, hãy gán request.Notes vào đây.
                _logger.LogInformation("Plan {PlanId} rejected by Expert {ExpertId}. Reason: {Notes}", plan.Id, expertId, request.Notes);
            }

            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId; // Cập nhật người chỉnh sửa lần cuối là chuyên gia

            _unitOfWork.Repository<ProductionPlan>().Update(plan);
            await _unitOfWork.Repository<ProductionPlan>().SaveChangesAsync();

            string action = request.Approved ? "Approved" : "Rejected";
            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' successfully {action}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving/rejecting production plan: {PlanId}", request.PlanId);

            return Result<Guid>.Failure("An error occurred during approval/rejection process.", "ApprovalRejectedFailed");
        }
    }
}