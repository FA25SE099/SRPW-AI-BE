using MediatR;
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
        try
        {
            // 1. Validate Category exists
            var category = await _unitOfWork.Repository<RiceVarietyCategory>()
                .FindAsync(c => c.Id == request.CategoryId);

            if (category == null)
            {
                return Result<Guid>.Failure(
                    $"Rice Variety Category with ID {request.CategoryId} not found.",
                    "CategoryNotFound");
            }

            // 2. Get current expert user
            var expertId = _currentUser.Id;
            if (expertId == null)
            {
                return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
            }

            // Verify user is actually an AgronomyExpert
            var expert = await _unitOfWork.AgronomyExpertRepository
                .FindAsync(e => e.Id == expertId.Value);

            if (expert == null)
            {
                return Result<Guid>.Failure(
                    "Only Agronomy Experts can create Standard Plans.",
                    "NotAnExpert");
            }

            // 3. Validate all MaterialIds exist
            var allMaterialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            if (allMaterialIds.Any())
            {
                var existingMaterials = await _unitOfWork.Repository<Material>()
                    .ListAsync(m => allMaterialIds.Contains(m.Id));

                var existingMaterialIds = existingMaterials.Select(m => m.Id).ToList();
                var missingMaterialIds = allMaterialIds.Except(existingMaterialIds).ToList();

                if (missingMaterialIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"Materials not found: {string.Join(", ", missingMaterialIds)}",
                        "MaterialsNotFound");
                }
            }

            // 4. Create the StandardPlan entity
            var standardPlan = new StandardPlan
            {
                CategoryId = request.CategoryId,
                ExpertId = expertId.Value,
                PlanName = request.PlanName,
                Description = request.Description,
                TotalDurationDays = request.TotalDurationDays,
                CreatedBy = expertId.Value,
                IsActive = request.IsActive
            };

            // 5. Create nested entities (Stages -> Tasks -> Materials)
            var stages = new List<StandardPlanStage>();
            var allTasks = new List<StandardPlanTask>();
            var allTaskMaterials = new List<StandardPlanTaskMaterial>();

            foreach (var stageDto in request.Stages.OrderBy(s => s.SequenceOrder))
            {
                // A. Create Stage
                var stage = new StandardPlanStage
                {
                    StandardPlan = standardPlan,
                    StageName = stageDto.StageName,
                    SequenceOrder = stageDto.SequenceOrder,
                    ExpectedDurationDays = stageDto.ExpectedDurationDays,
                    IsMandatory = stageDto.IsMandatory,
                    Notes = stageDto.Notes
                };

                stages.Add(stage);

                // B. Create Tasks for this Stage
                foreach (var taskDto in stageDto.Tasks.OrderBy(t => t.SequenceOrder))
                {
                    var task = new StandardPlanTask
                    {
                        StandardPlanStage = stage,
                        TaskName = taskDto.TaskName,
                        Description = taskDto.Description,
                        DaysAfter = taskDto.DaysAfter,
                        DurationDays = taskDto.DurationDays,
                        TaskType = taskDto.TaskType,
                        Priority = taskDto.Priority,
                        SequenceOrder = taskDto.SequenceOrder
                    };

                    allTasks.Add(task);

                    // C. Create Materials for this Task
                    foreach (var materialDto in taskDto.Materials)
                    {
                        var taskMaterial = new StandardPlanTaskMaterial
                        {
                            StandardPlanTask = task,
                            MaterialId = materialDto.MaterialId,
                            QuantityPerHa = materialDto.QuantityPerHa
                        };

                        allTaskMaterials.Add(taskMaterial);
                    }
                }
            }

            // 6. Add all entities to repositories
            await _unitOfWork.Repository<StandardPlan>().AddAsync(standardPlan);
            await _unitOfWork.Repository<StandardPlanStage>().AddRangeAsync(stages);
            await _unitOfWork.Repository<StandardPlanTask>().AddRangeAsync(allTasks);
            await _unitOfWork.Repository<StandardPlanTaskMaterial>().AddRangeAsync(allTaskMaterials);

            // 7. Save changes
            await _unitOfWork.Repository<StandardPlan>().SaveChangesAsync();

            // 8. Publish domain event
            await _mediator.Publish(
                new StandardPlanChangedEvent(standardPlan.Id, ChangeType.Created),
                cancellationToken);

            _logger.LogInformation(
                "Successfully created StandardPlan {PlanId} with {StageCount} stages, {TaskCount} tasks, and {MaterialCount} material entries by Expert {ExpertId}",
                standardPlan.Id, stages.Count, allTasks.Count, allTaskMaterials.Count, expertId);

            return Result<Guid>.Success(
                standardPlan.Id,
                $"Standard Plan '{standardPlan.PlanName}' created successfully with {stages.Count} stages and {allTasks.Count} tasks.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating standard plan: {PlanName}", request.PlanName);
            return Result<Guid>.Failure(
                "An error occurred while creating the standard plan.",
                "CreateStandardPlanFailed");
        }
    }
}

