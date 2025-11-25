using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Commands.CreateEmergencyProtocol;

public class CreateEmergencyProtocolCommandHandler : IRequestHandler<CreateEmergencyProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateEmergencyProtocolCommandHandler> _logger;

    public CreateEmergencyProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<CreateEmergencyProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateEmergencyProtocolCommand request, CancellationToken cancellationToken)
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
                    "Only Agronomy Experts can create Emergency Protocols.",
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

            // 4. Validate pest protocols exist (if specified in thresholds)
            var pestProtocolIds = request.Thresholds
                .Where(t => t.PestProtocolId.HasValue)
                .Select(t => t.PestProtocolId!.Value)
                .Distinct()
                .ToList();

            if (pestProtocolIds.Any())
            {
                var existingPestProtocolIds = await _unitOfWork.Repository<PestProtocol>()
                    .GetQueryable()
                    .Where(p => pestProtocolIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);

                var missingPestProtocolIds = pestProtocolIds.Except(existingPestProtocolIds).ToList();

                if (missingPestProtocolIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following pest protocols were not found: {string.Join(", ", missingPestProtocolIds)}",
                        "PestProtocolsNotFound");
                }
            }

            // 5. Validate weather protocols exist (if specified in thresholds)
            var weatherProtocolIds = request.Thresholds
                .Where(t => t.WeatherProtocolId.HasValue)
                .Select(t => t.WeatherProtocolId!.Value)
                .Distinct()
                .ToList();

            if (weatherProtocolIds.Any())
            {
                var existingWeatherProtocolIds = await _unitOfWork.Repository<WeatherProtocol>()
                    .GetQueryable()
                    .Where(w => weatherProtocolIds.Contains(w.Id))
                    .Select(w => w.Id)
                    .ToListAsync(cancellationToken);

                var missingWeatherProtocolIds = weatherProtocolIds.Except(existingWeatherProtocolIds).ToList();

                if (missingWeatherProtocolIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following weather protocols were not found: {string.Join(", ", missingWeatherProtocolIds)}",
                        "WeatherProtocolsNotFound");
                }
            }

            // 6. Validate rice varieties in thresholds exist
            var riceVarietyIds = request.Thresholds
                .Where(t => t.RiceVarietyId.HasValue)
                .Select(t => t.RiceVarietyId!.Value)
                .Distinct()
                .ToList();

            if (riceVarietyIds.Any())
            {
                var existingVarietyIds = await _unitOfWork.Repository<RiceVariety>()
                    .GetQueryable()
                    .Where(rv => riceVarietyIds.Contains(rv.Id))
                    .Select(rv => rv.Id)
                    .ToListAsync(cancellationToken);

                var missingVarietyIds = riceVarietyIds.Except(existingVarietyIds).ToList();

                if (missingVarietyIds.Any())
                {
                    return Result<Guid>.Failure(
                        $"The following rice varieties were not found: {string.Join(", ", missingVarietyIds)}",
                        "RiceVarietiesNotFound");
                }
            }

            // 7. Check for duplicate plan name within the same category
            var duplicateExists = await _unitOfWork.Repository<EmergencyProtocol>()
                .ExistsAsync(p => p.CategoryId == request.CategoryId &&
                              p.PlanName.ToLower() == request.PlanName.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"An Emergency Protocol with the name '{request.PlanName}' already exists for this category.",
                    "DuplicateProtocolName");
            }

            // 8. Validate all materials exist
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

            // 9. Create the EmergencyProtocol entity
            var emergencyProtocol = new EmergencyProtocol
            {
                CategoryId = request.CategoryId,
                ExpertId = expertId.Value,
                PlanName = request.PlanName.Trim(),
                Description = request.Description?.Trim(),
                TotalDurationDays = request.TotalDurationDays,
                CreatedBy = expertId.Value,
                IsActive = request.IsActive
            };

            // 10. Build nested entity hierarchy - Stages and Tasks
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

                emergencyProtocol.StandardPlanStages.Add(stage);
            }

            // 11. Add thresholds
            foreach (var thresholdDto in request.Thresholds)
            {
                var threshold = new Threshold
                {
                    PestProtocolId = thresholdDto.PestProtocolId,
                    WeatherProtocolId = thresholdDto.WeatherProtocolId,

                    // Pest threshold limits
                    PestAffectType = thresholdDto.PestAffectType?.Trim(),
                    PestSeverityLevel = thresholdDto.PestSeverityLevel?.Trim(),
                    PestAreaThresholdPercent = thresholdDto.PestAreaThresholdPercent,
                    PestPopulationThreshold = thresholdDto.PestPopulationThreshold?.Trim(),
                    PestDamageThresholdPercent = thresholdDto.PestDamageThresholdPercent,
                    PestGrowthStage = thresholdDto.PestGrowthStage?.Trim(),
                    PestThresholdNotes = thresholdDto.PestThresholdNotes?.Trim(),

                    // Weather threshold limits
                    WeatherEventType = thresholdDto.WeatherEventType?.Trim(),
                    WeatherIntensityLevel = thresholdDto.WeatherIntensityLevel?.Trim(),
                    WeatherMeasurementThreshold = thresholdDto.WeatherMeasurementThreshold,
                    WeatherMeasurementUnit = thresholdDto.WeatherMeasurementUnit?.Trim(),
                    WeatherThresholdOperator = thresholdDto.WeatherThresholdOperator?.Trim(),
                    WeatherDurationDaysThreshold = thresholdDto.WeatherDurationDaysThreshold,
                    WeatherThresholdNotes = thresholdDto.WeatherThresholdNotes?.Trim(),

                    // Common fields
                    ApplicableSeason = thresholdDto.ApplicableSeason?.Trim(),
                    RiceVarietyId = thresholdDto.RiceVarietyId,
                    Priority = thresholdDto.Priority,
                    GeneralNotes = thresholdDto.GeneralNotes?.Trim()
                };

                emergencyProtocol.Thresholds.Add(threshold);
            }

            // 12. Add and save with transaction
            await _unitOfWork.Repository<EmergencyProtocol>().AddAsync(emergencyProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 13. Publish domain event
            //await _mediator.Publish(
            //    new EmergencyProtocolCreatedEvent(emergencyProtocol.Id),
            //    cancellationToken);

            var totalTasks = emergencyProtocol.StandardPlanStages.Sum(s => s.StandardPlanTasks.Count);
            var totalMaterials = emergencyProtocol.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .Sum(t => t.StandardPlanTaskMaterials.Count);

            _logger.LogInformation(
                "Successfully created EmergencyProtocol {ProtocolId} '{ProtocolName}' with {StageCount} stages, {TaskCount} tasks, {ThresholdCount} thresholds, and {MaterialCount} material entries by Expert {ExpertId}",
                emergencyProtocol.Id, emergencyProtocol.PlanName, emergencyProtocol.StandardPlanStages.Count,
                totalTasks, emergencyProtocol.Thresholds.Count, totalMaterials, expertId);

            return Result<Guid>.Success(
                emergencyProtocol.Id,
                $"Emergency Protocol '{emergencyProtocol.PlanName}' created successfully with {emergencyProtocol.StandardPlanStages.Count} stages, {totalTasks} tasks, and {emergencyProtocol.Thresholds.Count} thresholds.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while creating emergency protocol '{ProtocolName}' for Expert {ExpertId}",
                request.PlanName, expertId);

            return Result<Guid>.Failure(
                "A database error occurred while saving the emergency protocol. Please check your data and try again.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating emergency protocol '{ProtocolName}' for Expert {ExpertId}",
                request.PlanName, expertId);

            return Result<Guid>.Failure(
                "An unexpected error occurred while creating the emergency protocol.",
                "CreateEmergencyProtocolFailed");
        }
    }
}