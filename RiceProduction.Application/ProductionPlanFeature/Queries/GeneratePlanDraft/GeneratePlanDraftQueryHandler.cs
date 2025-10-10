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
            // ... (Steps 1, 2, 3 remain the same: Get Area, Get Standard Plan, Get Material Prices) ...

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
                    .Include(sp => sp.StandardPlanStages)
                        .ThenInclude(sps => sps.StandardPlanTasks)
                            .ThenInclude(spt => spt.StandardPlanTaskMaterials)
                                .ThenInclude(sptm => sptm.Material)
            );

            if (standardPlan == null)
            {
                return Result<GeneratePlanDraftResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            // --- 3. Get Material Prices based on BasePlantingDate ---
            var materialIds = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );

            var materialPrices = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);
            
            // --- 4. Build the Draft Response Structure and Calculate Costs ---
            
            decimal totalPlanCost = 0M; // <--- Khởi tạo biến tích lũy tổng chi phí

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
            foreach (var standardStage in standardPlan.StandardPlanStages.OrderBy(s => s.SequenceOrder))
            {
                var stageResponse = new ProductionStageResponse
                {
                    StageName = standardStage.StandardPlan?.PlanName ?? "Stage",
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
                        if (!materialPrices.TryGetValue(standardMaterial.MaterialId, out var unitPrice))
                        {
                            unitPrice = 0M;
                        }
                        
                        // Calculation: EstimatedAmount = QuantityPerHa * effectiveTotalArea * PricePerMaterial
                        decimal totalQuantity = standardMaterial.QuantityPerHa * effectiveTotalArea;
                        decimal estimatedAmount = totalQuantity * unitPrice;
                        
                        taskTotalEstimatedCost += estimatedAmount;
                        totalPlanCost += estimatedAmount; // <--- Tích lũy vào tổng chi phí Plan
                        
                        materialsResponse.Add(new ProductionPlanTaskMaterialResponse
                        {
                            MaterialId = standardMaterial.MaterialId,
                            MaterialName = standardMaterial.Material.Name,
                            MaterialUnit = standardMaterial.Material.Unit,
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
            response.EstimatedTotalPlanCost = totalPlanCost; // <--- Gán tổng chi phí cuối cùng

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
