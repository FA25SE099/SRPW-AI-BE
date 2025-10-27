using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.StandardPlanFeature.Queries.ReviewStandardPlan;

public class ReviewStandardPlanQueryHandler : 
    IRequestHandler<ReviewStandardPlanQuery, Result<StandardPlanReviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReviewStandardPlanQueryHandler> _logger;

    public ReviewStandardPlanQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<ReviewStandardPlanQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<StandardPlanReviewDto>> Handle(
        ReviewStandardPlanQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Reviewing standard plan ID: {StandardPlanId} with SowDate: {SowDate}, Area: {Area} ha",
                request.StandardPlanId, request.SowDate, request.AreaInHectares);

            // Load Standard Plan with all related entities
            var standardPlan = await _unitOfWork.Repository<StandardPlan>().FindAsync(
                match: sp => sp.Id == request.StandardPlanId && sp.IsActive,
                includeProperties: q => q
                    .Include(sp => sp.Category)
                    .Include(sp => sp.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                        .ThenInclude(stage => stage.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                            .ThenInclude(task => task.StandardPlanTaskMaterials)
                                .ThenInclude(material => material.Material)
            );

            if (standardPlan == null)
            {
                _logger.LogWarning("Standard plan with ID {StandardPlanId} not found or inactive", request.StandardPlanId);
                return Result<StandardPlanReviewDto>.Failure(
                    $"Standard plan with ID {request.StandardPlanId} not found or is inactive.",
                    "StandardPlanNotFound");
            }

            // Get material prices based on sow date
            var materialIds = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.SowDate.Date, DateTimeKind.Utc);

            // Fetch latest prices valid up to the sow date
            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate
            );

            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => new { p.PricePerMaterial, p.ValidFrom });

            // Calculate timeline and costs
            decimal totalPlanCost = 0M;
            decimal totalMaterialQuantity = 0M;
            var stagesReview = new List<StandardPlanStageReviewDto>();

            DateTime? earliestTaskDate = null;
            DateTime? latestTaskDate = null;

            foreach (var standardStage in standardPlan.StandardPlanStages.OrderBy(s => s.SequenceOrder))
            {
                var tasksReview = new List<StandardPlanTaskReviewDto>();
                DateTime? stageStartDate = null;
                DateTime? stageEndDate = null;

                foreach (var standardTask in standardStage.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                {
                    decimal taskTotalCost = 0M;
                    var scheduledStartDate = request.SowDate.AddDays(standardTask.DaysAfter);
                    var scheduledEndDate = scheduledStartDate.AddDays(standardTask.DurationDays - 1);

                    // Track stage dates
                    if (stageStartDate == null || scheduledStartDate < stageStartDate)
                        stageStartDate = scheduledStartDate;
                    if (stageEndDate == null || scheduledEndDate > stageEndDate)
                        stageEndDate = scheduledEndDate;

                    // Track overall timeline
                    if (earliestTaskDate == null || scheduledStartDate < earliestTaskDate)
                        earliestTaskDate = scheduledStartDate;
                    if (latestTaskDate == null || scheduledEndDate > latestTaskDate)
                        latestTaskDate = scheduledEndDate;

                    var materialsReview = new List<StandardPlanTaskMaterialReviewDto>();

                    // Calculate material requirements
                    foreach (var standardMaterial in standardTask.StandardPlanTaskMaterials)
                    {
                        var material = standardMaterial.Material;
                        decimal quantityPerHa = standardMaterial.QuantityPerHa;
                        decimal totalQuantity = quantityPerHa * request.AreaInHectares;
                        totalMaterialQuantity += totalQuantity;

                        decimal? unitPrice = null;
                        decimal? totalCost = null;
                        DateTime? priceDate = null;

                        if (materialPriceMap.TryGetValue(standardMaterial.MaterialId, out var priceInfo))
                        {
                            unitPrice = priceInfo.PricePerMaterial;
                            priceDate = priceInfo.ValidFrom;

                            // Calculate cost considering amount per material unit
                            decimal amountPerUnit = material.AmmountPerMaterial.GetValueOrDefault(1M);
                            decimal unitsNeeded = Math.Ceiling(quantityPerHa / amountPerUnit);
                            decimal pricePerHa = unitsNeeded * unitPrice.Value;
                            totalCost = pricePerHa * request.AreaInHectares;

                            taskTotalCost += totalCost.Value;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "No price found for material {MaterialId} on or before {Date}",
                                standardMaterial.MaterialId, priceReferenceDate);
                        }

                        materialsReview.Add(new StandardPlanTaskMaterialReviewDto
                        {
                            MaterialId = material.Id,
                            MaterialName = material.Name,
                            MaterialType = material.Type,
                            MaterialUnit = material.Unit,
                            QuantityPerHa = quantityPerHa,
                            TotalQuantityNeeded = totalQuantity,
                            UnitPrice = unitPrice,
                            TotalCost = totalCost,
                            PriceDate = priceDate
                        });
                    }

                    totalPlanCost += taskTotalCost;

                    tasksReview.Add(new StandardPlanTaskReviewDto
                    {
                        TaskId = standardTask.Id,
                        TaskName = standardTask.TaskName,
                        Description = standardTask.Description,
                        TaskType = standardTask.TaskType,
                        Priority = standardTask.Priority,
                        SequenceOrder = standardTask.SequenceOrder,
                        DaysAfterSowing = standardTask.DaysAfter,
                        DurationDays = standardTask.DurationDays,
                        ScheduledStartDate = scheduledStartDate,
                        ScheduledEndDate = scheduledEndDate,
                        EstimatedTaskCost = taskTotalCost,
                        EstimatedTaskCostPerHa = request.AreaInHectares > 0 
                            ? taskTotalCost / request.AreaInHectares 
                            : 0M,
                        Materials = materialsReview
                    });
                }

                stagesReview.Add(new StandardPlanStageReviewDto
                {
                    StageId = standardStage.Id,
                    StageName = standardStage.StageName,
                    SequenceOrder = standardStage.SequenceOrder,
                    ExpectedDurationDays = standardStage.ExpectedDurationDays,
                    Notes = standardStage.Notes,
                    EstimatedStartDate = stageStartDate,
                    EstimatedEndDate = stageEndDate,
                    Tasks = tasksReview
                });
            }

            // Build response
            var response = new StandardPlanReviewDto
            {
                StandardPlanId = standardPlan.Id,
                PlanName = standardPlan.PlanName,
                Description = standardPlan.Description,
                CategoryId = standardPlan.CategoryId,
                CategoryName = standardPlan.Category.CategoryName,
                SowDate = request.SowDate,
                AreaInHectares = request.AreaInHectares,
                EstimatedStartDate = earliestTaskDate ?? request.SowDate,
                EstimatedEndDate = latestTaskDate ?? request.SowDate.AddDays(standardPlan.TotalDurationDays),
                TotalDurationDays = standardPlan.TotalDurationDays,
                EstimatedTotalCost = totalPlanCost,
                EstimatedCostPerHectare = request.AreaInHectares > 0 
                    ? totalPlanCost / request.AreaInHectares 
                    : 0M,
                Stages = stagesReview,
                TotalStages = standardPlan.StandardPlanStages.Count,
                TotalTasks = standardPlan.StandardPlanStages.SelectMany(s => s.StandardPlanTasks).Count(),
                TotalMaterialTypes = materialIds.Count,
                TotalMaterialQuantity = totalMaterialQuantity
            };

            _logger.LogInformation(
                "Successfully reviewed standard plan. ID: {StandardPlanId}, Total Cost: {Cost}, Duration: {Duration} days",
                response.StandardPlanId, response.EstimatedTotalCost, response.TotalDurationDays);

            return Result<StandardPlanReviewDto>.Success(
                response,
                "Successfully generated standard plan review.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error reviewing standard plan ID: {StandardPlanId}", 
                request.StandardPlanId);
            return Result<StandardPlanReviewDto>.Failure(
                "Failed to review standard plan.",
                "ReviewStandardPlanFailed");
        }
    }
}

