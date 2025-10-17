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
            // Load Plan with all required relationships (deep includes)
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.PlanId,
                includeProperties: q => q
                    .Include(p => p.Group).ThenInclude(g => g!.Cluster) // Group và Cluster
                    .Include(p => p.Group).ThenInclude(g => g!.Plots) // Plots thuộc Group
                    .Include(p => p.CurrentProductionStages) // Stages
                        .ThenInclude(s => s.ProductionPlanTasks.OrderBy(t => t.SequenceOrder)) // Tasks trong Stages
                            .ThenInclude(t => t.ProductionPlanTaskMaterials) // Materials trong Tasks
                                .ThenInclude(m => m.Material) // Chi tiết Material
            );

            if (plan == null)
            {
                return Result<ExpertPlanDetailResponse>.Failure($"Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            // Map Group Details
            var groupDetails = plan.Group != null ? new ExpertPlanGroupDetailResponse
            {
                Id = plan.Group.Id,
                ClusterName = plan.Group.Cluster?.ClusterName ?? "N/A",
                TotalArea = plan.Group.TotalArea,
                Status = plan.Group.Status,
                PlotNames = plan.Group.Plots.Select(p => p.Id.ToString()).ToList()
            } : null;

            // Calculate total cost for the entire plan
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

                        estimatedTotalPlanCost += t.EstimatedMaterialCost;

                        return new ExpertPlanTaskResponse
                        {
                            Id = t.Id,
                            TaskName = t.TaskName,
                            Description = t.Description,
                            TaskType = t.TaskType,
                            ScheduledDate = t.ScheduledDate,
                            Priority = t.Priority,
                            SequenceOrder = t.SequenceOrder,
                            EstimatedMaterialCost = t.EstimatedMaterialCost,
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
                EstimatedTotalPlanCost = estimatedTotalPlanCost // Total cost calculated above
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
