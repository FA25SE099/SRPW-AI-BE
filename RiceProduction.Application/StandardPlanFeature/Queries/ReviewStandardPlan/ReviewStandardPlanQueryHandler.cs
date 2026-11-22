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
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ReviewStandardPlanQueryHandler> _logger;

    public ReviewStandardPlanQueryHandler(
        IApplicationDbContext context,
        ILogger<ReviewStandardPlanQueryHandler> logger)
    {
        _context = context;
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

            var standardPlan = await _context.StandardPlans
                .AsNoTracking()
                .Include(sp => sp.Category)
                .Include(sp => sp.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                    .ThenInclude(stage => stage.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                        .ThenInclude(task => task.StandardPlanTaskMaterials)
                            .ThenInclude(material => material.Material)
                .AsSplitQuery()
                .FirstOrDefaultAsync(sp => sp.Id == request.StandardPlanId && sp.IsActive, cancellationToken);

            if (standardPlan == null)
            {
                _logger.LogWarning("Standard plan with ID {StandardPlanId} not found or inactive", request.StandardPlanId);
                return Result<StandardPlanReviewDto>.Failure(
                    $"Standard plan with ID {request.StandardPlanId} not found or is inactive.",
                    "StandardPlanNotFound");
            }

            var materialIds = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.SowDate.Date, DateTimeKind.Utc);
            var materialPriceMap = await GetLatestMaterialPricesAsync(materialIds, priceReferenceDate, cancellationToken);

            var stagesReview = standardPlan.StandardPlanStages
                .OrderBy(s => s.SequenceOrder)
                .Select(stage => BuildStageReview(stage, request.SowDate, request.AreaInHectares, materialPriceMap, priceReferenceDate))
                .ToList();

            var allTasks = stagesReview.SelectMany(s => s.Tasks).ToList();
            var totalPlanCost = allTasks.Sum(t => t.EstimatedTaskCost);
            var totalMaterialQuantity = allTasks.SelectMany(t => t.Materials).Sum(m => m.TotalQuantityNeeded);
            DateTime? earliestTaskDate = allTasks.Count > 0 ? allTasks.Min(t => t.ScheduledStartDate) : null;
            DateTime? latestTaskDate = allTasks.Count > 0 ? allTasks.Max(t => t.ScheduledEndDate) : null;

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
                EstimatedCostPerHectare = CalculateCostPerHectare(totalPlanCost, request.AreaInHectares),
                Stages = stagesReview,
                TotalStages = standardPlan.StandardPlanStages.Count,
                TotalTasks = allTasks.Count,
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

    private async Task<Dictionary<Guid, MaterialPrice>> GetLatestMaterialPricesAsync(
        List<Guid> materialIds,
        DateTime priceReferenceDate,
        CancellationToken cancellationToken)
    {
        if (materialIds.Count == 0)
        {
            return new Dictionary<Guid, MaterialPrice>();
        }

        var potentialPrices = await _context.MaterialPrices
            .AsNoTracking()
            .Where(p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate)
            .ToListAsync(cancellationToken);

        return potentialPrices
            .GroupBy(p => p.MaterialId)
            .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
            .ToDictionary(p => p.MaterialId);
    }

    private StandardPlanStageReviewDto BuildStageReview(
        StandardPlanStage standardStage,
        DateTime sowDate,
        decimal areaInHectares,
        Dictionary<Guid, MaterialPrice> materialPriceMap,
        DateTime priceReferenceDate)
    {
        var tasksReview = standardStage.StandardPlanTasks
            .OrderBy(t => t.SequenceOrder)
            .Select(task => BuildTaskReview(task, sowDate, areaInHectares, materialPriceMap, priceReferenceDate))
            .ToList();

        return new StandardPlanStageReviewDto
        {
            StageId = standardStage.Id,
            StageName = standardStage.StageName,
            SequenceOrder = standardStage.SequenceOrder,
            ExpectedDurationDays = standardStage.ExpectedDurationDays,
            Notes = standardStage.Notes,
            EstimatedStartDate = tasksReview.Count > 0 ? tasksReview.Min(t => t.ScheduledStartDate) : null,
            EstimatedEndDate = tasksReview.Count > 0 ? tasksReview.Max(t => t.ScheduledEndDate) : null,
            Tasks = tasksReview
        };
    }

    private StandardPlanTaskReviewDto BuildTaskReview(
        StandardPlanTask standardTask,
        DateTime sowDate,
        decimal areaInHectares,
        Dictionary<Guid, MaterialPrice> materialPriceMap,
        DateTime priceReferenceDate)
    {
        var scheduledStartDate = sowDate.AddDays(standardTask.DaysAfter);
        var scheduledEndDate = scheduledStartDate.AddDays(standardTask.DurationDays - 1);

        var materialsReview = standardTask.StandardPlanTaskMaterials
            .Select(m => BuildMaterialReview(m, areaInHectares, materialPriceMap, priceReferenceDate))
            .ToList();

        var taskTotalCost = materialsReview.Sum(m => m.TotalCost ?? 0M);

        return new StandardPlanTaskReviewDto
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
            EstimatedTaskCostPerHa = CalculateCostPerHectare(taskTotalCost, areaInHectares),
            Materials = materialsReview
        };
    }

    private StandardPlanTaskMaterialReviewDto BuildMaterialReview(
        StandardPlanTaskMaterial standardMaterial,
        decimal areaInHectares,
        Dictionary<Guid, MaterialPrice> materialPriceMap,
        DateTime priceReferenceDate)
    {
        var material = standardMaterial.Material;
        var quantityPerHa = standardMaterial.QuantityPerHa;
        var totalQuantity = quantityPerHa * areaInHectares;

        decimal? unitPrice = null;
        decimal? totalCost = null;
        DateTime? priceDate = null;

        if (materialPriceMap.TryGetValue(standardMaterial.MaterialId, out var priceInfo))
        {
            unitPrice = priceInfo.PricePerMaterial;
            priceDate = priceInfo.ValidFrom;

            var amountPerUnit = material.AmmountPerMaterial.GetValueOrDefault(1M);
            var unitsNeeded = Math.Ceiling(quantityPerHa / amountPerUnit);
            var pricePerHa = unitsNeeded * unitPrice.Value;
            totalCost = pricePerHa * areaInHectares;
        }
        else
        {
            _logger.LogWarning(
                "No price found for material {MaterialId} on or before {Date}",
                standardMaterial.MaterialId, priceReferenceDate);
        }

        return new StandardPlanTaskMaterialReviewDto
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
        };
    }

    private static decimal CalculateCostPerHectare(decimal totalCost, decimal areaInHectares) =>
        areaInHectares > 0 ? totalCost / areaInHectares : 0M;
}

