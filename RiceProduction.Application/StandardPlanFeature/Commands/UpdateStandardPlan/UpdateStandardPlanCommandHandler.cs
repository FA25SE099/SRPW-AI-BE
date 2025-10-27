using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.StandardPlanFeature.Commands.UpdateStandardPlan;

public class UpdateStandardPlanCommandHandler : IRequestHandler<UpdateStandardPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateStandardPlanCommandHandler> _logger;

    public UpdateStandardPlanCommandHandler(
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<UpdateStandardPlanCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        UpdateStandardPlanCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating standard plan {StandardPlanId}", request.StandardPlanId);

            var standardPlan = await _unitOfWork.Repository<StandardPlan>()
                .FindAsync(sp => sp.Id == request.StandardPlanId);

            if (standardPlan == null)
            {
                _logger.LogWarning("Standard plan {StandardPlanId} not found", request.StandardPlanId);
                return Result<Guid>.Failure(
                    $"Standard plan with ID {request.StandardPlanId} not found.",
                    "StandardPlanNotFound");
            }

            standardPlan.PlanName = request.PlanName;
            standardPlan.Description = request.Description;
            standardPlan.TotalDurationDays = request.TotalDurationDays;
            
            var statusChanged = standardPlan.IsActive != request.IsActive;
            standardPlan.IsActive = request.IsActive;

            _unitOfWork.Repository<StandardPlan>().Update(standardPlan);
            await _unitOfWork.Repository<StandardPlan>().SaveChangesAsync();

            var changeType = statusChanged ? ChangeType.StatusChanged : ChangeType.Updated;
            await _mediator.Publish(
                new StandardPlanChangedEvent(standardPlan.Id, changeType),
                cancellationToken);

            _logger.LogInformation(
                "Successfully updated standard plan {StandardPlanId}",
                standardPlan.Id);

            return Result<Guid>.Success(
                standardPlan.Id,
                $"Standard plan '{standardPlan.PlanName}' updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating standard plan {StandardPlanId}", request.StandardPlanId);
            return Result<Guid>.Failure(
                "Failed to update standard plan.",
                "UpdateStandardPlanFailed");
        }
    }
}
