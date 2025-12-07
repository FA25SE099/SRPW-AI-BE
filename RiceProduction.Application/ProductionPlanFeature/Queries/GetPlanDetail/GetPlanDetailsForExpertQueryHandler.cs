using System;
using System.Collections.Generic;
using System.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Entities;
namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanDetail;

public class GetPlanDetailsForExpertQueryHandler : 
    IRequestHandler<GetPlanDetailsForExpertQuery, Result<ExpertPlanDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlanDetailsForExpertQueryHandler> _logger;

    public GetPlanDetailsForExpertQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPlanDetailsForExpertQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ExpertPlanDetailResponse>> Handle(GetPlanDetailsForExpertQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.PlanId,
                includeProperties: q => q
                    .Include(p => p.Group).ThenInclude(g => g!.Cluster)
                    .Include(p => p.Group).ThenInclude(g => g!.Plots) 
                        .ThenInclude(plot => plot.Farmer)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks.OrderBy(t => t.SequenceOrder))
                            .ThenInclude(t => t.ProductionPlanTaskMaterials)
                                .ThenInclude(m => m.Material)
            );

            if (plan == null)
            {
                return Result<ExpertPlanDetailResponse>.Failure($"Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }
            
            // Map Plot Details
            var plotsResponse = plan.Group?.Plots
                .Select(plot => new ExpertPlotResponse
                {
                    Id = plot.Id,
                    Area = plot.Area,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    SoilType = plot.SoilType,
                    Status = plot.Status,
                    FarmerId = plot.FarmerId
                })
                .ToList() ?? new List<ExpertPlotResponse>();

            // Map Group Details
            var groupDetails = plan.Group != null ? new ExpertPlanGroupDetailResponse
            {
                Id = plan.Group.Id,
                ClusterName = plan.Group.Cluster?.ClusterName ?? "N/A",
                TotalArea = plan.Group.TotalArea,
                Status = plan.Group.Status,
                Plots = plotsResponse // Gán danh sách Plots chi tiết
            } : null;

            // Calculate total cost and map stages/tasks/materials
            decimal estimatedTotalPlanCost = 0M;

            var stagesResponse = plan.CurrentProductionStages
                .OrderBy(s => s.SequenceOrder)
                .Select(s =>
                {
                    var tasksResponse = s.ProductionPlanTasks.Select(t =>
                    {
                        var materialsResponse = t.ProductionPlanTaskMaterials.Select(m => new ExpertPlanTaskMaterialResponse
                        {
                            MaterialId = m.MaterialId,
                            MaterialName = m.Material.Name,
                            MaterialUnit = m.Material.Unit,
                            QuantityPerHa = m.QuantityPerHa,
                            EstimatedAmount = m.EstimatedAmount.GetValueOrDefault(0M)
                        }).ToList();

                        // Calculate material cost from detail to verify stored value
                        decimal calculatedMaterialCost = t.ProductionPlanTaskMaterials
                            .Sum(m => m.EstimatedAmount.GetValueOrDefault(0M));

                        // Verify stored cost matches calculated cost
                        if (Math.Abs(t.EstimatedMaterialCost - calculatedMaterialCost) > 0.01M)
                        {
                            _logger.LogWarning(
                                "Material cost mismatch for task {TaskId} '{TaskName}': " +
                                "Stored={StoredCost:C}, Calculated={CalculatedCost:C}",
                                t.Id, t.TaskName, t.EstimatedMaterialCost, calculatedMaterialCost);
                        }

                        // Use stored value with null safety
                        decimal taskMaterialCost = t.EstimatedMaterialCost;
                        estimatedTotalPlanCost += taskMaterialCost;

                        return new ExpertPlanTaskResponse
                        {
                            Id = t.Id,
                            TaskName = t.TaskName,
                            Description = t.Description,
                            TaskType = t.TaskType,
                            ScheduledDate = t.ScheduledDate,
                            Priority = t.Priority,
                            SequenceOrder = t.SequenceOrder,
                            EstimatedMaterialCost = taskMaterialCost,
                            Materials = materialsResponse
                        };
                    }).ToList();

                    return new ExpertPlanStageResponse
                    {
                        Id = s.Id,
                        StageName = s.StageName,
                        SequenceOrder = s.SequenceOrder,
                        TypicalDurationDays = s.TypicalDurationDays,
                        ColorCode = s.ColorCode,
                        Tasks = tasksResponse
                    };
                }).ToList();

            // Log cost calculation summary
            _logger.LogInformation(
                "Plan {PlanId} cost calculation: Total={TotalCost:C}, Area={Area}ha, Stages={StageCount}, Tasks={TaskCount}",
                plan.Id, estimatedTotalPlanCost, plan.TotalArea, 
                stagesResponse.Count, stagesResponse.Sum(s => s.Tasks.Count));

            var response = new ExpertPlanDetailResponse
            {
                Id = plan.Id,
                PlanName = plan.PlanName,
                StandardPlanId = plan.StandardPlanId,
                GroupId = plan.GroupId,
                TotalArea = plan.TotalArea,
                BasePlantingDate = plan.BasePlantingDate,
                Status = plan.Status,
                GroupDetails = groupDetails,
                Stages = stagesResponse,
                EstimatedTotalPlanCost = estimatedTotalPlanCost
            };

            return Result<ExpertPlanDetailResponse>.Success(response, "Successfully retrieved plan details for expert review.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plan details for expert. Plan ID: {PlanId}", request.PlanId);
            return Result<ExpertPlanDetailResponse>.Failure("Failed to retrieve plan details.", "GetPlanDetailsFailed");
        }
    }
}
