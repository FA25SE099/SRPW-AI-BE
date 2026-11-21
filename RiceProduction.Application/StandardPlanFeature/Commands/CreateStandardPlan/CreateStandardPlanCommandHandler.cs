using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Events;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.StandardPlanFeature.Commands.CreateStandardPlan;

public class CreateStandardPlanCommandHandler : IRequestHandler<CreateStandardPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateStandardPlanCommandHandler> _logger;

    public CreateStandardPlanCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<CreateStandardPlanCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateStandardPlanCommand request, CancellationToken cancellationToken)
    {
        var expertId = _currentUser.Id;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
        }

        try
        {
            var validationError = await ValidateRequestAsync(request, expertId.Value, cancellationToken);
            if (validationError != null)
            {
                return validationError;
            }

            var standardPlan = BuildStandardPlan(request, expertId.Value);

            await _unitOfWork.Repository<StandardPlan>().AddAsync(standardPlan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _mediator.Publish(
                new StandardPlanChangedEvent(standardPlan.Id, ChangeType.Created),
                cancellationToken);

            var totalTasks = standardPlan.StandardPlanStages.Sum(s => s.StandardPlanTasks.Count);
            var totalMaterials = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .Sum(t => t.StandardPlanTaskMaterials.Count);

            _logger.LogInformation(
                "Successfully created StandardPlan {PlanId} '{PlanName}' with {StageCount} stages, {TaskCount} tasks, and {MaterialCount} material entries by Expert {ExpertId}",
                standardPlan.Id, standardPlan.PlanName, standardPlan.StandardPlanStages.Count,
                totalTasks, totalMaterials, expertId);

            return Result<Guid>.Success(
                standardPlan.Id,
                $"Standard Plan '{standardPlan.PlanName}' created successfully with {standardPlan.StandardPlanStages.Count} stages and {totalTasks} tasks.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while creating standard plan '{PlanName}' for Expert {ExpertId}",
                request.PlanName, expertId);

            return Result<Guid>.Failure(
                "A database error occurred while saving the standard plan. Please check your data and try again.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating standard plan '{PlanName}' for Expert {ExpertId}",
                request.PlanName, expertId);

            return Result<Guid>.Failure(
                "An unexpected error occurred while creating the standard plan.",
                "CreateStandardPlanFailed");
        }
    }

    private async Task<Result<Guid>?> ValidateRequestAsync(
        CreateStandardPlanCommand request,
        Guid expertId,
        CancellationToken cancellationToken)
    {
        var expertExists = await _unitOfWork.AgronomyExpertRepository
            .ExistAsync(expertId, cancellationToken);

        if (!expertExists)
        {
            return Result<Guid>.Failure(
                "Only Agronomy Experts can create Standard Plans.",
                "NotAnExpert");
        }

        var categoryExists = await _unitOfWork.Repository<RiceVarietyCategory>()
            .ExistsAsync(c => c.Id == request.CategoryId);

        if (!categoryExists)
        {
            return Result<Guid>.Failure(
                $"Rice Variety Category with ID {request.CategoryId} not found.",
                "CategoryNotFound");
        }

        var duplicateExists = await _unitOfWork.Repository<StandardPlan>()
            .ExistsAsync(p => p.CategoryId == request.CategoryId &&
                              p.PlanName.ToLower() == request.PlanName.ToLower());

        if (duplicateExists)
        {
            return Result<Guid>.Failure(
                $"A Standard Plan with the name '{request.PlanName}' already exists for this category.",
                "DuplicatePlanName");
        }

        var allMaterialIds = request.Stages
            .SelectMany(s => s.Tasks)
            .SelectMany(t => t.Materials)
            .Select(m => m.MaterialId)
            .Distinct()
            .ToList();

        if (allMaterialIds.Count == 0)
        {
            return null;
        }

        var existingMaterialIds = await _unitOfWork.Repository<Material>()
            .GetQueryable()
            .Where(m => allMaterialIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var missingMaterialIds = allMaterialIds.Except(existingMaterialIds).ToList();
        if (missingMaterialIds.Count > 0)
        {
            return Result<Guid>.Failure(
                $"The following materials were not found: {string.Join(", ", missingMaterialIds)}",
                "MaterialsNotFound");
        }

        return null;
    }

    private static StandardPlan BuildStandardPlan(CreateStandardPlanCommand request, Guid expertId)
    {
        var standardPlan = new StandardPlan
        {
            CategoryId = request.CategoryId,
            ExpertId = expertId,
            PlanName = request.PlanName.Trim(),
            Description = request.Description?.Trim(),
            TotalDurationDays = request.TotalDurationDays,
            CreatedBy = expertId,
            IsActive = request.IsActive
        };

        foreach (var stageDto in request.Stages.OrderBy(s => s.SequenceOrder))
        {
            var stage = new StandardPlanStage
            {
                StageName = stageDto.StageName.Trim(),
                SequenceOrder = stageDto.SequenceOrder,
                ExpectedDurationDays = stageDto.ExpectedDurationDays,
                IsMandatory = stageDto.IsMandatory,
                Notes = stageDto.Notes?.Trim()
            };

            foreach (var taskDto in stageDto.Tasks.OrderBy(t => t.SequenceOrder))
            {
                var task = new StandardPlanTask
                {
                    TaskName = taskDto.TaskName.Trim(),
                    Description = taskDto.Description?.Trim(),
                    DaysAfter = taskDto.DaysAfter,
                    DurationDays = taskDto.DurationDays,
                    TaskType = taskDto.TaskType,
                    Priority = taskDto.Priority,
                    SequenceOrder = taskDto.SequenceOrder
                };

                foreach (var materialDto in taskDto.Materials)
                {
                    task.StandardPlanTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        MaterialId = materialDto.MaterialId,
                        QuantityPerHa = materialDto.QuantityPerHa
                    });
                }

                stage.StandardPlanTasks.Add(task);
            }

            standardPlan.StandardPlanStages.Add(stage);
        }

        return standardPlan;
    }
}

