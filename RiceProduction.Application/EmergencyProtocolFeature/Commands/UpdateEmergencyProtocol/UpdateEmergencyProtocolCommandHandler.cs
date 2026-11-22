using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Common;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Commands.UpdateEmergencyProtocol;

public class UpdateEmergencyProtocolCommandHandler : IRequestHandler<UpdateEmergencyProtocolCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly ILogger<UpdateEmergencyProtocolCommandHandler> _logger;

    public UpdateEmergencyProtocolCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        ILogger<UpdateEmergencyProtocolCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
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
            var emergencyProtocol = await _context.EmergencyProtocols
                .Include(ep => ep.StandardPlanStages)
                    .ThenInclude(s => s.StandardPlanTasks)
                        .ThenInclude(t => t.StandardPlanTaskMaterials)
                .Include(ep => ep.Thresholds)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ep => ep.Id == request.EmergencyProtocolId, cancellationToken);

            if (emergencyProtocol == null)
            {
                return Result<Guid>.Failure(
                    "Emergency Protocol not found.",
                    "NotFound");
            }

            // 2. Validate category exists
            var categoryExists = await _context.RiceVarietyCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (!categoryExists)
            {
                return Result<Guid>.Failure(
                    $"Rice Variety Category with ID {request.CategoryId} not found.",
                    "CategoryNotFound");
            }

            // 3. Check for duplicate plan name (excluding current protocol)
            var duplicateExists = await _context.EmergencyProtocols
                .AsNoTracking()
                .AnyAsync(p => p.Id != request.EmergencyProtocolId &&
                              p.CategoryId == request.CategoryId &&
                              p.PlanName.ToLower() == request.PlanName.ToLower(),
                    cancellationToken);

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"An Emergency Protocol with the name '{request.PlanName}' already exists for this category.",
                    "DuplicateProtocolName");
            }

            var pestProtocolIds = request.Thresholds
                .Where(t => t.PestProtocolId.HasValue)
                .Select(t => t.PestProtocolId!.Value)
                .Distinct()
                .ToList();

            var pestValidation = await ValidateEntitiesExistAsync<PestProtocol>(
                pestProtocolIds, "pest protocols", "PestProtocolsNotFound", cancellationToken);
            if (pestValidation != null)
            {
                return pestValidation;
            }

            var weatherProtocolIds = request.Thresholds
                .Where(t => t.WeatherProtocolId.HasValue)
                .Select(t => t.WeatherProtocolId!.Value)
                .Distinct()
                .ToList();

            var weatherValidation = await ValidateEntitiesExistAsync<WeatherProtocol>(
                weatherProtocolIds, "weather protocols", "WeatherProtocolsNotFound", cancellationToken);
            if (weatherValidation != null)
            {
                return weatherValidation;
            }

            var allMaterialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var materialValidation = await ValidateEntitiesExistAsync<Material>(
                allMaterialIds, "materials", "MaterialsNotFound", cancellationToken);
            if (materialValidation != null)
            {
                return materialValidation;
            }

            emergencyProtocol.CategoryId = request.CategoryId;
            emergencyProtocol.PlanName = request.PlanName.Trim();
            emergencyProtocol.Description = request.Description?.Trim();
            emergencyProtocol.TotalDurationDays = request.TotalDurationDays;
            emergencyProtocol.IsActive = request.IsActive;

            _context.StandardPlanStages.RemoveRange(emergencyProtocol.StandardPlanStages);
            _context.Thresholds.RemoveRange(emergencyProtocol.Thresholds);

            ReplaceStages(emergencyProtocol, request);
            ReplaceThresholds(emergencyProtocol, request);

            _context.EmergencyProtocols.Update(emergencyProtocol);
            await _context.SaveChangesAsync(cancellationToken);

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

    private async Task<Result<Guid>?> ValidateEntitiesExistAsync<TEntity>(
        IReadOnlyCollection<Guid> ids,
        string entityDisplayName,
        string errorCode,
        CancellationToken cancellationToken)
        where TEntity : BaseAuditableEntity
    {
        if (ids.Count == 0)
        {
            return null;
        }

        var existingIds = await _context.Set<TEntity>()
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var missingIds = ids.Except(existingIds).ToList();
        if (missingIds.Count == 0)
        {
            return null;
        }

        return Result<Guid>.Failure(
            $"The following {entityDisplayName} were not found: {string.Join(", ", missingIds)}",
            errorCode);
    }

    private static void ReplaceStages(EmergencyProtocol emergencyProtocol, UpdateEmergencyProtocolCommand request)
    {
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
                    task.StandardPlanTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        MaterialId = materialDto.MaterialId,
                        QuantityPerHa = materialDto.QuantityPerHa
                    });
                }

                stage.StandardPlanTasks.Add(task);
            }

            emergencyProtocol.StandardPlanStages.Add(stage);
        }
    }

    private static void ReplaceThresholds(EmergencyProtocol emergencyProtocol, UpdateEmergencyProtocolCommand request)
    {
        emergencyProtocol.Thresholds.Clear();

        foreach (var thresholdDto in request.Thresholds)
        {
            emergencyProtocol.Thresholds.Add(new Threshold
            {
                PestProtocolId = thresholdDto.PestProtocolId,
                WeatherProtocolId = thresholdDto.WeatherProtocolId,

                PestAffectType = thresholdDto.PestAffectType?.Trim(),
                PestSeverityLevel = thresholdDto.PestSeverityLevel?.Trim(),
                PestAreaThresholdPercent = thresholdDto.PestAreaThresholdPercent,
                PestPopulationThreshold = thresholdDto.PestPopulationThreshold?.Trim(),
                PestDamageThresholdPercent = thresholdDto.PestDamageThresholdPercent,
                PestGrowthStage = thresholdDto.PestGrowthStage?.Trim(),
                PestThresholdNotes = thresholdDto.PestThresholdNotes?.Trim(),

                WeatherEventType = thresholdDto.WeatherEventType?.Trim(),
                WeatherIntensityLevel = thresholdDto.WeatherIntensityLevel?.Trim(),
                WeatherMeasurementThreshold = thresholdDto.WeatherMeasurementThreshold,
                WeatherMeasurementUnit = thresholdDto.WeatherMeasurementUnit?.Trim(),
                WeatherThresholdOperator = thresholdDto.WeatherThresholdOperator?.Trim(),
                WeatherDurationDaysThreshold = thresholdDto.WeatherDurationDaysThreshold,
                WeatherThresholdNotes = thresholdDto.WeatherThresholdNotes?.Trim(),

                ApplicableSeason = thresholdDto.ApplicableSeason?.Trim(),
                RiceVarietyId = thresholdDto.RiceVarietyId,
                Priority = thresholdDto.Priority,
                GeneralNotes = thresholdDto.GeneralNotes?.Trim()
            });
        }
    }
}
