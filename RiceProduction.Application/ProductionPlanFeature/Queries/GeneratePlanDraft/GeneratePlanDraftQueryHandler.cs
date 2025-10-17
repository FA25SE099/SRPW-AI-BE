using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GeneratePlanDraft;

public class GeneratePlanDraftQueryHandler : 
    IRequestHandler<GeneratePlanDraftQuery, Result<GeneratePlanDraftResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GeneratePlanDraftQueryHandler> _logger;

    public GeneratePlanDraftQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GeneratePlanDraftQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<GeneratePlanDraftResponse>> Handle(GeneratePlanDraftQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // --- 1. Get Group Area ---
            var group = await _unitOfWork.Repository<Group>().FindAsync(g => g.Id == request.GroupId);

            if (group == null)
            {
                return Result<GeneratePlanDraftResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }
            
            if (group.TotalArea == null || group.TotalArea.Value <= 0)
            {
                return Result<GeneratePlanDraftResponse>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
            }
            
            decimal effectiveTotalArea = group.TotalArea.Value;

            // --- 2. Get Standard Plan (Template) Data ---
            var standardPlan = await _unitOfWork.Repository<StandardPlan>().FindAsync(
                match: sp => sp.Id == request.StandardPlanId,
                includeProperties: q => q
                    .Include(sp => sp.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                        .ThenInclude(sps => sps.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                            .ThenInclude(spt => spt.StandardPlanTaskMaterials)
                                .ThenInclude(sptm => sptm.Material)
            );

            if (standardPlan == null)
            {
                return Result<GeneratePlanDraftResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            // --- 3. Get Material Prices based on BasePlantingDate (FIXED LOGIC) ---
            var materialIds = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            // Fetch all potential prices valid up to the reference date
            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );

            // Group by MaterialId and select the price with the latest ValidFrom date
            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);

            // Fetch Material Details (AmmountPerMaterial) separately to map them correctly in the loop
            // FIX: Dùng Distinct() trên Material object trước khi tạo Dictionary để tránh trùng khóa.
            var materialDetailsMap = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .Select(m => m.Material) // Lấy Material object
                .Distinct() // Chỉ lấy các Material duy nhất
                .ToDictionary(m => m.Id, m => m); // Key là MaterialId, Value là Material Entity
            
            // --- 4. Build the Draft Response Structure and Calculate Costs ---
            
            decimal totalPlanCost = 0M; 

            var response = new GeneratePlanDraftResponse
            {
                StandardPlanId = standardPlan.Id,
                GroupId = request.GroupId,
                PlanName = $"{standardPlan.PlanName} - {group.Id.ToString().Substring(0, 8)}",
                TotalArea = effectiveTotalArea,
                BasePlantingDate = request.BasePlantingDate,
            };

            var stagesResponse = new List<ProductionStageResponse>();
            
            // Loop through Standard Stages
            foreach (var standardStage in standardPlan.StandardPlanStages)
            {
                var stageResponse = new ProductionStageResponse
                {
                    // Lấy PlanName từ StandardPlan gốc (PlanName)
                    StageName = standardPlan.PlanName, 
                    SequenceOrder = standardStage.SequenceOrder,
                    Description = standardStage.Notes,
                    TypicalDurationDays = standardStage.ExpectedDurationDays,
                };
                
                var tasksResponse = new List<ProductionPlanTaskResponse>();

                // Loop through Standard Tasks
                foreach (var standardTask in standardStage.StandardPlanTasks.OrderBy(t => t.DaysAfter))
                {
                    decimal taskTotalEstimatedCost = 0;
                    var scheduledDate = request.BasePlantingDate.AddDays(standardTask.DaysAfter);
                    
                    var taskResponse = new ProductionPlanTaskResponse
                    {
                        TaskName = standardTask.TaskName,
                        Description = standardTask.Description,
                        TaskType = standardTask.TaskType,
                        Priority = standardTask.Priority,
                        SequenceOrder = standardTask.SequenceOrder,
                        ScheduledDate = scheduledDate,
                        ScheduledEndDate = scheduledDate.AddDays(standardTask.DurationDays - 1),
                    };
                    
                    var materialsResponse = new List<ProductionPlanTaskMaterialResponse>();

                    // Loop through Standard Task Materials and Calculate Cost
                    foreach (var standardMaterial in standardTask.StandardPlanTaskMaterials)
                    {
                        decimal unitPrice = materialPriceMap.GetValueOrDefault(standardMaterial.MaterialId, 0M);
                        
                        // FIX: Lấy Material Detail từ Map mới tạo
                        if (!materialDetailsMap.TryGetValue(standardMaterial.MaterialId, out var materialDetail))
                        {
                            _logger.LogWarning("Material details not found for Material ID {MId}.", standardMaterial.MaterialId);
                            continue; // Bỏ qua vật tư này nếu không tìm thấy chi tiết
                        }
                        
                        if (unitPrice == 0M)
                        {
                            _logger.LogWarning("Price not found for Material ID {MId} on date {Date}.", standardMaterial.MaterialId, priceReferenceDate.ToShortDateString());
                        }

                        // Tính toán chi phí:
                        decimal amountPerUnit = materialDetail.AmmountPerMaterial.GetValueOrDefault(1M);
                        
                        // PricePerHa = (QuantityPerHa / AmmountPerMaterial) * PricePerMaterial
                        decimal pricePerHa = (standardMaterial.QuantityPerHa / amountPerUnit) * unitPrice;
                        
                        decimal estimatedAmount = pricePerHa * effectiveTotalArea;
                        
                        taskTotalEstimatedCost += estimatedAmount;
                        totalPlanCost += estimatedAmount; 
                        
                        materialsResponse.Add(new ProductionPlanTaskMaterialResponse
                        {
                            MaterialId = standardMaterial.MaterialId,
                            MaterialName = materialDetail.Name,
                            MaterialUnit = materialDetail.Unit,
                            QuantityPerHa = standardMaterial.QuantityPerHa,
                            EstimatedAmount = estimatedAmount
                        });
                    }

                    taskResponse.EstimatedMaterialCost = taskTotalEstimatedCost;
                    taskResponse.Materials = materialsResponse;
                    tasksResponse.Add(taskResponse);
                }

                stageResponse.Tasks = tasksResponse;
                stagesResponse.Add(stageResponse);
            }
            
            response.Stages = stagesResponse;
            response.EstimatedTotalPlanCost = totalPlanCost; 

            _logger.LogInformation("Successfully generated draft ProductionPlan from StandardPlan ID {SPId} for Group ID {GId}. Total Cost: {Cost}", request.StandardPlanId, request.GroupId, totalPlanCost);

            return Result<GeneratePlanDraftResponse>.Success(response, "Successfully generated production plan draft.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating production plan draft from StandardPlan ID {SPId} for Group ID {GId}.", request.StandardPlanId, request.GroupId);

            return Result<GeneratePlanDraftResponse>.Failure("An error occurred while generating the plan draft.", "GeneratePlanDraft failed.");
        }
    }
}
