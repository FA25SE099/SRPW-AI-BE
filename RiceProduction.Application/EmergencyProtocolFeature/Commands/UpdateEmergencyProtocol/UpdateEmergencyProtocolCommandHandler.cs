using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Commands.UpdateEmergencyProtocol;

public class UpdateEmergencyProtocolCommandHandler : IRequestHandler<UpdateEmergencyProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateEmergencyProtocolCommandHandler> _logger;

    public UpdateEmergencyProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<UpdateEmergencyProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateEmergencyProtocolCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;
        if (!userId.HasValue)
        {
            return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
        }

        try
        {
            // 1. Get existing emergency protocol with all related data
            var emergencyProtocol = await _unitOfWork.Repository<EmergencyProtocol>()
                .GetQueryable()
                .Include(ep => ep.StandardPlanStages)
                    .ThenInclude(s => s.StandardPlanTasks)
                        .ThenInclude(t => t.StandardPlanTaskMaterials)
                .Include(ep => ep.Thresholds)
                .FirstOrDefaultAsync(ep => ep.Id == request.EmergencyProtocolId, cancellationToken);

            if (emergencyProtocol == null)
            {
                return Result<Guid>.Failure(
                    "Emergency Protocol not found.",
                    "NotFound");
            }

            // 2. Validate category exists
            var categoryExists = await _unitOfWork.Repository<RiceVarietyCategory>()
                .ExistsAsync(c => c.Id == request.CategoryId);

            if (!categoryExists)
            {
                return Result<Guid>.Failure(
                    $"Rice Variety Category with ID {request.CategoryId} not found.",
                    "CategoryNotFound");
            }

            // 3. Check for duplicate plan name (excluding current protocol)
            var duplicateExists = await _unitOfWork.Repository<EmergencyProtocol>()
                .ExistsAsync(p => p.Id != request.EmergencyProtocolId &&
                              p.CategoryId == request.CategoryId &&
                              p.PlanName.ToLower() == request.PlanName.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"An Emergency Protocol with the name '{request.PlanName}' already exists for this category.",
                    "DuplicateProtocolName");
            }

            // 4. Validate pest protocols
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

            // 5. Validate weather protocols
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

            // 6. Validate materials
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

            // 7. Update basic properties
            emergencyProtocol.CategoryId = request.CategoryId;
            emergencyProtocol.PlanName = request.PlanName.Trim();
            emergencyProtocol.Description = request.Description?.Trim();
            emergencyProtocol.TotalDurationDays = request.TotalDurationDays;
            emergencyProtocol.IsActive = request.IsActive;

            // 8. Remove all existing stages and thresholds
            var stageRepo = _unitOfWork.Repository<StandardPlanStage>();
            stageRepo.DeleteRange(emergencyProtocol.StandardPlanStages);

            var thresholdRepo = _unitOfWork.Repository<Threshold>();
            thresholdRepo.DeleteRange(emergencyProtocol.Thresholds);

            // 9. Add new stages
            emergencyProtocol.StandardPlanStages.Clear();
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

            // 10. Add new thresholds
            emergencyProtocol.Thresholds.Clear();
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

            // 11. Update and save
            _unitOfWork.Repository<EmergencyProtocol>().Update(emergencyProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);


            _logger.LogInformation(
                "Successfully updated EmergencyProtocol {ProtocolId} by User {UserId}",
                emergencyProtocol.Id, userId);

            return Result<Guid>.Success(
                emergencyProtocol.Id,
                $"Emergency Protocol '{emergencyProtocol.PlanName}' updated successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while updating emergency protocol {ProtocolId}",
                request.EmergencyProtocolId);

            return Result<Guid>.Failure(
                "A database error occurred while updating the emergency protocol.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating emergency protocol {ProtocolId}",
                request.EmergencyProtocolId);

            return Result<Guid>.Failure(
                "An unexpected error occurred while updating the emergency protocol.",
                "UpdateEmergencyProtocolFailed");
        }
    }
}
