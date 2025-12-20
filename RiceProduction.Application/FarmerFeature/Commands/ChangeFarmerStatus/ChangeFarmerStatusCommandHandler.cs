using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.FarmerFeature.Commands.ChangeFarmerStatus;

public class ChangeFarmerStatusCommandHandler : IRequestHandler<ChangeFarmerStatusCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangeFarmerStatusCommandHandler> _logger;

    public ChangeFarmerStatusCommandHandler(
        IUnitOfWork _unitOfWork,
        ILogger<ChangeFarmerStatusCommandHandler> logger)
    {
        this._unitOfWork = _unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        ChangeFarmerStatusCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get farmer using FarmerRepository
            var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId, cancellationToken);

            if (farmer == null)
            {
                return Result<Guid>.Failure(
                    "Farmer not found.",
                    "NotFound");
            }

            var oldStatus = farmer.Status;

            // Validate status transition
            if (oldStatus == request.NewStatus)
            {
                return Result<Guid>.Failure(
                    $"Farmer is already in {request.NewStatus} status.",
                    "InvalidOperation");
            }

            // Update status
            farmer.Status = request.NewStatus;

            // Log the status change
            _logger.LogInformation(
                "Changing farmer {FarmerId} status from {OldStatus} to {NewStatus}. Reason: {Reason}",
                request.FarmerId,
                oldStatus,
                request.NewStatus,
                request.Reason ?? "Not provided");

            // If changing to NotAllowed or Resigned, deactivate assignments
            if (request.NewStatus == FarmerStatus.NotAllowed || request.NewStatus == FarmerStatus.Resigned)
            {
                var assignments = await _unitOfWork.Repository<SupervisorFarmerAssignment>()
                    .ListAsync(sfa => sfa.FarmerId == request.FarmerId && sfa.IsActive);

                foreach (var assignment in assignments)
                {
                    assignment.IsActive = false;
                    assignment.AssignmentNotes = $"Deactivated due to farmer status change to {request.NewStatus}. {request.Reason}";
                }

                if (assignments.Any())
                {
                    _logger.LogInformation(
                        "Deactivated {Count} supervisor assignments for farmer {FarmerId}",
                        assignments.Count,
                        request.FarmerId);
                }
            }

            // If changing back to Normal
            if (request.NewStatus == FarmerStatus.Normal && 
                (oldStatus == FarmerStatus.NotAllowed || oldStatus == FarmerStatus.Resigned))
            {
                _logger.LogInformation(
                    "Farmer {FarmerId} status restored to Normal. Supervisor may need to reassign.",
                    request.FarmerId);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var message = $"Farmer status successfully changed from {oldStatus} to {request.NewStatus}.";
            
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                message += $" Reason: {request.Reason}";
            }

            _logger.LogInformation(
                "Successfully changed farmer {FarmerId} status to {NewStatus}",
                request.FarmerId,
                request.NewStatus);

            return Result<Guid>.Success(farmer.Id, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error changing status for farmer {FarmerId}",
                request.FarmerId);
            return Result<Guid>.Failure(
                "An error occurred while changing farmer status.",
                "ChangeFarmerStatusFailed");
        }
    }
}
