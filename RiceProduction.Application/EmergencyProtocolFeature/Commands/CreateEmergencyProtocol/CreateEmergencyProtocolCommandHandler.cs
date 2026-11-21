using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Request.ThresholdRequests;
using RiceProduction.Domain.Common;
using RiceProduction.Domain.Entities;

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
        var expertId = _currentUser.Id;
        if (!expertId.HasValue)
        {
            return Result<Guid>.Failure("User not authenticated.", "Unauthorized");
        }

        try
        {
            var expertExists = await _unitOfWork.AgronomyExpertRepository
                .ExistAsync(expertId.Value, cancellationToken);

            if (!expertExists)
            {
                return Result<Guid>.Failure(
                    "Only Agronomy Experts can create Emergency Protocols.",
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

            var pestValidation = await ValidateEntitiesExistAsync<PestProtocol>(
                request.Thresholds.Where(t => t.PestProtocolId.HasValue).Select(t => t.PestProtocolId!.Value),
                "pest protocols",
                "PestProtocolsNotFound",
                cancellationToken);
            if (pestValidation != null)
            {
                return pestValidation;
            }

            var weatherValidation = await ValidateEntitiesExistAsync<WeatherProtocol>(
                request.Thresholds.Where(t => t.WeatherProtocolId.HasValue).Select(t => t.WeatherProtocolId!.Value),
                "weather protocols",
                "WeatherProtocolsNotFound",
                cancellationToken);
            if (weatherValidation != null)
            {
                return weatherValidation;
            }

            var varietyValidation = await ValidateEntitiesExistAsync<RiceVariety>(
                request.Thresholds.Where(t => t.RiceVarietyId.HasValue).Select(t => t.RiceVarietyId!.Value),
                "rice varieties",
                "RiceVarietiesNotFound",
                cancellationToken);
            if (varietyValidation != null)
            {
                return varietyValidation;
            }

            var duplicateExists = await _unitOfWork.Repository<EmergencyProtocol>()
                .ExistsAsync(p => p.CategoryId == request.CategoryId &&
                              p.PlanName.ToLower() == request.PlanName.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"An Emergency Protocol with the name '{request.PlanName}' already exists for this category.",
                    "DuplicateProtocolName");
            }

            var materialValidation = await ValidateEntitiesExistAsync<Material>(
                request.Stages.SelectMany(s => s.Tasks).SelectMany(t => t.Materials).Select(m => m.MaterialId),
                "materials",
                "MaterialsNotFound",
                cancellationToken);
            if (materialValidation != null)
            {
                return materialValidation;
            }

            var emergencyProtocol = BuildEmergencyProtocol(request, expertId.Value);

            await _unitOfWork.Repository<EmergencyProtocol>().AddAsync(emergencyProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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

    private async Task<Result<Guid>?> ValidateEntitiesExistAsync<TEntity>(
        IEnumerable<Guid> ids,
        string entityPluralName,
        string errorCode,
        CancellationToken cancellationToken)
        where TEntity : BaseAuditableEntity
    {
        var distinctIds = ids.Distinct().ToList();
        if (!distinctIds.Any())
        {
            return null;
        }

        var existingIds = await _unitOfWork.Repository<TEntity>()
            .GetQueryable()
            .Where(e => distinctIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var missingIds = distinctIds.Except(existingIds).ToList();
        if (!missingIds.Any())
        {
            return null;
        }

        return Result<Guid>.Failure(
            $"The following {entityPluralName} were not found: {string.Join(", ", missingIds)}",
            errorCode);
    }

    private static EmergencyProtocol BuildEmergencyProtocol(CreateEmergencyProtocolCommand request, Guid expertId)
    {
        var emergencyProtocol = new EmergencyProtocol
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
            emergencyProtocol.StandardPlanStages.Add(MapStage(stageDto));
        }

        foreach (var thresholdDto in request.Thresholds)
        {
            emergencyProtocol.Thresholds.Add(MapThreshold(thresholdDto));
        }

        return emergencyProtocol;
    }

    private static StandardPlanStage MapStage(StandardPlanStageRequest stageDto)
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

        return stage;
    }

    private static Threshold MapThreshold(ThresholdRequest thresholdDto)
    {
        return new Threshold
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
        };
    }
}