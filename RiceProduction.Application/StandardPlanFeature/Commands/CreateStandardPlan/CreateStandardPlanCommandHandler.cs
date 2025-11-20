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
        // 1. Get and validate current user
        var expertId = _currentUser.Id;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
        }

        try
        {
            // 2. Validate expert exists
            var expertExists = await _unitOfWork.AgronomyExpertRepository
                .ExistAsync(expertId.Value, cancellationToken);

            if (!expertExists)
            {
                return Result<Guid>.Failure(
                    "Only Agronomy Experts can create Standard Plans.",
                    "NotAnExpert");
            }

            // 3. Validate category exists
            var categoryExists = await _unitOfWork.Repository<RiceVarietyCategory>()
                .ExistsAsync(c => c.Id == request.CategoryId);

            if (!categoryExists)
            {
                return Result<Guid>.Failure(
                    $"Rice Variety Category with ID {request.CategoryId} not found.",
                    "CategoryNotFound");
            }

            // 4. Check for duplicate plan name within the same category
            var duplicateExists = await _unitOfWork.Repository<StandardPlan>()
                .ExistsAsync(p => p.CategoryId == request.CategoryId && 
                              p.PlanName.ToLower() == request.PlanName.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Standard Plan with the name '{request.PlanName}' already exists for this category.",
                    "DuplicatePlanName");
            }

            // 5. Validate all materials exist
            var allMaterialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (allMaterialIds.Any())
            {
                var existingMaterialIds = await _unitOfWork.Repository<Material>()
                    .GetQueryable()
                    .Where(m => allMaterialIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(cancellationToken);

                var missingMaterialIds = allMaterialIds.Except(existingMaterialIds).ToList();

                if (missingMaterialIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following materials were not found: {string.Join(", ", missingMaterialIds)}",
                        "MaterialsNotFound");
                }
            }

            // 6. Create the StandardPlan entity with all nested entities
            var standardPlan = new StandardPlan
            {
                CategoryId = request.CategoryId,
                ExpertId = expertId.Value,
                PlanName = request.PlanName.Trim(),
                Description = request.Description?.Trim(),
                TotalDurationDays = request.TotalDurationDays,
                CreatedBy = expertId.Value,
                IsActive = request.IsActive
            };

            // 7. Build nested entity hierarchy
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
                        var taskMaterial = new StandardPlanTaskMaterial
                        {
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa
                        };

                        task.StandardPlanTaskMaterials.Add(taskMaterial);
                    }

                    stage.StandardPlanTasks.Add(task);
                }

                standardPlan.StandardPlanStages.Add(stage);
            }

            // 8. Add and save with transaction
            await _unitOfWork.Repository<StandardPlan>().AddAsync(standardPlan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 9. Publish domain event
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
}

