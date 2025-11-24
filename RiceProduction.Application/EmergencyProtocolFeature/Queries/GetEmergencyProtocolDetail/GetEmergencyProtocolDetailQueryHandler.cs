using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.EmergencyProtocolFeature.Queries.GetEmergencyProtocolDetail;

public class GetEmergencyProtocolDetailQueryHandler : IRequestHandler<GetEmergencyProtocolDetailQuery, Result<EmergencyProtocolDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetEmergencyProtocolDetailQueryHandler> _logger;

    public GetEmergencyProtocolDetailQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetEmergencyProtocolDetailQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<EmergencyProtocolDetailDto>> Handle(
        GetEmergencyProtocolDetailQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting emergency protocol detail for ID: {ProtocolId}",
                request.EmergencyProtocolId);

            var emergencyProtocol = await _unitOfWork.Repository<EmergencyProtocol>()
                .GetQueryable()
                .Include(ep => ep.Category)
                .Include(ep => ep.Creator)
                .Include(ep => ep.Thresholds)
                    .ThenInclude(t => t.PestProtocol)
                .Include(ep => ep.Thresholds)
                    .ThenInclude(t => t.WeatherProtocol)
                .Include(ep => ep.Thresholds)
                    .ThenInclude(t => t.RiceVariety)
                .Include(ep => ep.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                    .ThenInclude(s => s.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                        .ThenInclude(t => t.StandardPlanTaskMaterials)
                            .ThenInclude(m => m.Material)
                .FirstOrDefaultAsync(ep => ep.Id == request.EmergencyProtocolId, cancellationToken);

            if (emergencyProtocol == null)
            {
                return Result<EmergencyProtocolDetailDto>.Failure(
                    "Emergency Protocol not found.",
                    "NotFound");
            }

            // Get current material prices
            var currentDate = DateTime.Now;
            var materialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync();

            // Manual mapping
            var detailDto = new EmergencyProtocolDetailDto
            {
                Id = emergencyProtocol.Id,
                PlanName = emergencyProtocol.PlanName,
                Description = emergencyProtocol.Description,
                TotalDurationDays = emergencyProtocol.TotalDurationDays,
                IsActive = emergencyProtocol.IsActive,
                CategoryId = emergencyProtocol.CategoryId,
                CategoryName = emergencyProtocol.Category?.CategoryName ?? string.Empty,
                CreatedBy = emergencyProtocol.CreatedBy,
                CreatorName = emergencyProtocol.Creator?.FullName ?? string.Empty,
                CreatedAt = emergencyProtocol.CreatedAt,
                LastModified = emergencyProtocol.LastModified,

                // Map thresholds
                Thresholds = emergencyProtocol.Thresholds?
                    .Select(t => new EmergencyThresholdDetailDto
                    {
                        Id = t.Id,
                        PestProtocolId = t.PestProtocolId,
                        PestProtocolName = t.PestProtocol?.Name,
                        WeatherProtocolId = t.WeatherProtocolId,
                        WeatherProtocolName = t.WeatherProtocol?.Name,
                        
                        // Pest threshold limits
                        PestAffectType = t.PestAffectType,
                        PestSeverityLevel = t.PestSeverityLevel,
                        PestAreaThresholdPercent = t.PestAreaThresholdPercent,
                        PestPopulationThreshold = t.PestPopulationThreshold,
                        PestDamageThresholdPercent = t.PestDamageThresholdPercent,
                        PestGrowthStage = t.PestGrowthStage,
                        PestThresholdNotes = t.PestThresholdNotes,
                        
                        // Weather threshold limits
                        WeatherEventType = t.WeatherEventType,
                        WeatherIntensityLevel = t.WeatherIntensityLevel,
                        WeatherMeasurementThreshold = t.WeatherMeasurementThreshold,
                        WeatherMeasurementUnit = t.WeatherMeasurementUnit,
                        WeatherThresholdOperator = t.WeatherThresholdOperator,
                        WeatherDurationDaysThreshold = t.WeatherDurationDaysThreshold,
                        WeatherThresholdNotes = t.WeatherThresholdNotes,
                        
                        // Common fields
                        ApplicableSeason = t.ApplicableSeason,
                        RiceVarietyId = t.RiceVarietyId,
                        RiceVarietyName = t.RiceVariety?.VarietyName,
                        Priority = t.Priority,
                        GeneralNotes = t.GeneralNotes
                    })
                    .ToList() ?? new List<EmergencyThresholdDetailDto>(),

                // Map stages
                Stages = emergencyProtocol.StandardPlanStages?
                    .Select(stage => new EmergencyProtocolStageDetailDto
                    {
                        Id = stage.Id,
                        StageName = stage.StageName,
                        SequenceOrder = stage.SequenceOrder,
                        ExpectedDurationDays = stage.ExpectedDurationDays,
                        IsMandatory = stage.IsMandatory,
                        Notes = stage.Notes,
                        Tasks = stage.StandardPlanTasks?
                            .Select(task => new EmergencyProtocolTaskDetailDto
                            {
                                Id = task.Id,
                                TaskName = task.TaskName,
                                Description = task.Description,
                                DaysAfter = task.DaysAfter,
                                DurationDays = task.DurationDays,
                                TaskType = task.TaskType.ToString(),
                                Priority = task.Priority.ToString(),
                                SequenceOrder = task.SequenceOrder,
                                Materials = task.StandardPlanTaskMaterials?
                                    .Select(material =>
                                    {
                                        var currentPrice = materialPrices
                                            .Where(p => p.MaterialId == material.MaterialId &&
                                                       p.Material.IsActive &&
                                                       p.ValidFrom.CompareTo(currentDate) <= 0 &&
                                                       (!p.ValidTo.HasValue || p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))
                                            .OrderByDescending(p => p.ValidFrom)
                                            .FirstOrDefault()?.PricePerMaterial ?? 0;

                                        return new EmergencyProtocolTaskMaterialDetailDto
                                        {
                                            Id = material.Id,
                                            MaterialId = material.MaterialId,
                                            MaterialName = material.Material?.Name ?? string.Empty,
                                            QuantityPerHa = material.QuantityPerHa,
                                            Unit = material.Material?.Unit,
                                            PricePerUnit = currentPrice,
                                            EstimatedCostPerHa = material.QuantityPerHa * currentPrice
                                        };
                                    })
                                    .ToList() ?? new List<EmergencyProtocolTaskMaterialDetailDto>()
                            })
                            .ToList() ?? new List<EmergencyProtocolTaskDetailDto>()
                    })
                    .ToList() ?? new List<EmergencyProtocolStageDetailDto>()
            };

            // Calculate totals
            detailDto.TotalThresholds = detailDto.Thresholds.Count;
            detailDto.TotalStages = detailDto.Stages.Count;
            detailDto.TotalTasks = detailDto.Stages.Sum(s => s.Tasks.Count);
            detailDto.TotalMaterialTypes = detailDto.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .Count();

            _logger.LogInformation(
                "Successfully retrieved emergency protocol detail for ID: {ProtocolId}",
                request.EmergencyProtocolId);

            return Result<EmergencyProtocolDetailDto>.Success(
                detailDto,
                "Emergency Protocol detail retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error occurred while getting emergency protocol detail for ID: {ProtocolId}",
                request.EmergencyProtocolId);
            return Result<EmergencyProtocolDetailDto>.Failure(
                $"An error occurred while retrieving emergency protocol detail: {ex.Message}");
        }
    }
}
