using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;

public class EditPlanCommandHandler :
    IRequestHandler<EditPlanCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<EditPlanCommandHandler> _logger;

    public EditPlanCommandHandler(
        IApplicationDbContext context,
        ILogger<EditPlanCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(EditPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var expertId = request.ExpertId;
            if (expertId == null)
            {
                return Result<Guid>.Failure("Current expert user ID not found.", "AuthenticationRequired");
            }

            var plan = await _context.ProductionPlans
                .Include(p => p.Group)
                .Include(p => p.CurrentProductionStages)
                    .ThenInclude(s => s.ProductionPlanTasks)
                        .ThenInclude(t => t.ProductionPlanTaskMaterials)
                .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

            if (plan == null)
            {
                return Result<Guid>.Failure($"Production Plan with ID {request.PlanId} not found.", "PlanNotFound");
            }

            if (plan.Status != TaskStatus.Draft && plan.Status != TaskStatus.PendingApproval)
            {
                return Result<Guid>.Failure("Plan can only be edited when in Draft or Submitted status.", "InvalidStatusForEdit");
            }

            var effectiveTotalArea = ResolveEffectiveTotalArea(plan);
            if (effectiveTotalArea <= 0M)
            {
                return Result<Guid>.Failure("Plan area is undefined. Cannot recalculate costs.", "AreaMissing");
            }

            plan.PlanName = request.PlanName;
            plan.BasePlantingDate = DateTime.SpecifyKind(request.BasePlantingDate, DateTimeKind.Utc);
            plan.LastModified = DateTime.UtcNow;
            plan.LastModifiedBy = expertId;

            var materialIds = request.Stages
                .SelectMany(s => s.Tasks)
                .SelectMany(t => t.Materials)
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            var potentialPrices = await _context.MaterialPrices
                .AsNoTracking()
                .Where(p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate)
                .ToListAsync(cancellationToken);
            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => p.PricePerMaterial);

            var materialDetailsList = await _context.Materials
                .AsNoTracking()
                .Where(m => materialIds.Contains(m.Id))
                .ToListAsync(cancellationToken);
            var materialDetailMap = materialDetailsList.ToDictionary(m => m.Id, m => m);

            RemoveExistingStages(plan);

            plan.CurrentProductionStages = BuildStages(
                plan, request, expertId, effectiveTotalArea, materialPriceMap, materialDetailMap);

            _context.ProductionPlans.Update(plan);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(plan.Id, $"Production Plan '{plan.PlanName}' updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing production plan: {PlanId}", request.PlanId);

            if (ex is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23503", TableName: "ProductionPlanTaskMaterials" } })
            {
                return Result<Guid>.Failure("Validation failed: One or more Material IDs do not exist.", "MaterialIdInvalid");
            }

            return Result<Guid>.Failure("An error occurred while editing the production plan.", "EditProductionPlanFailed");
        }
    }

    private static decimal ResolveEffectiveTotalArea(ProductionPlan plan)
    {
        var effectiveTotalArea = plan.TotalArea ?? 0M;
        if (effectiveTotalArea <= 0M && plan.GroupId.HasValue && plan.Group != null)
        {
            effectiveTotalArea = plan.Group.TotalArea.GetValueOrDefault(0M);
        }

        return effectiveTotalArea;
    }

    private void RemoveExistingStages(ProductionPlan plan)
    {
        var oldStages = plan.CurrentProductionStages.ToList();
        if (!oldStages.Any())
        {
            return;
        }

        var oldTasks = oldStages.SelectMany(s => s.ProductionPlanTasks).ToList();
        var oldMaterials = oldTasks.SelectMany(t => t.ProductionPlanTaskMaterials).ToList();

        _context.ProductionPlanTaskMaterials.RemoveRange(oldMaterials);
        _context.ProductionPlanTasks.RemoveRange(oldTasks);
        _context.ProductionStages.RemoveRange(oldStages);

        plan.CurrentProductionStages.Clear();
    }

    private static List<ProductionStage> BuildStages(
        ProductionPlan plan,
        EditPlanCommand request,
        Guid? expertId,
        decimal effectiveTotalArea,
        Dictionary<Guid, decimal> materialPriceMap,
        Dictionary<Guid, Material> materialDetailMap)
    {
        var newStages = new List<ProductionStage>();

        foreach (var stageDto in request.Stages.OrderBy(s => s.SequenceOrder))
        {
            var newStage = new ProductionStage
            {
                ProductionPlan = plan,
                StageName = stageDto.StageName,
                Description = stageDto.Description,
                SequenceOrder = stageDto.SequenceOrder,
                TypicalDurationDays = stageDto.TypicalDurationDays,
                ColorCode = stageDto.ColorCode,
                IsActive = true,
                LastModifiedBy = expertId
            };

            foreach (var taskDto in stageDto.Tasks.OrderBy(t => t.SequenceOrder))
            {
                var scheduledDateUtc = DateTime.SpecifyKind(taskDto.ScheduledDate, DateTimeKind.Utc);
                var scheduledEndDateUtc = taskDto.ScheduledEndDate.HasValue
                    ? DateTime.SpecifyKind(taskDto.ScheduledEndDate.Value, DateTimeKind.Utc)
                    : (DateTime?)null;

                var newTask = new ProductionPlanTask
                {
                    ProductionStage = newStage,
                    TaskName = taskDto.TaskName,
                    Description = taskDto.Description,
                    TaskType = taskDto.TaskType,
                    ScheduledDate = scheduledDateUtc,
                    ScheduledEndDate = scheduledEndDateUtc,
                    Priority = taskDto.Priority,
                    SequenceOrder = taskDto.SequenceOrder,
                    Status = TaskStatus.Draft, // Reset task status on major edit
                    LastModifiedBy = expertId
                };

                decimal totalTaskMaterialCost = 0M;

                foreach (var materialDto in taskDto.Materials)
                {
                    var unitPrice = materialPriceMap.GetValueOrDefault(materialDto.MaterialId, 0M);
                    materialDetailMap.TryGetValue(materialDto.MaterialId, out var materialDetail);

                    var estimatedAmount = CalculateMaterialEstimatedAmount(
                        materialDto.QuantityPerHa, materialDetail, unitPrice, effectiveTotalArea);

                    newTask.ProductionPlanTaskMaterials.Add(new ProductionPlanTaskMaterial
                    {
                        MaterialId = materialDto.MaterialId,
                        QuantityPerHa = materialDto.QuantityPerHa,
                        ProductionPlanTask = newTask,
                        LastModifiedBy = expertId,
                        EstimatedAmount = estimatedAmount
                    });

                    totalTaskMaterialCost += estimatedAmount;
                }

                newTask.EstimatedMaterialCost = totalTaskMaterialCost;
                newStage.ProductionPlanTasks.Add(newTask);
            }

            newStages.Add(newStage);
        }

        return newStages;
    }

    private static decimal CalculateMaterialEstimatedAmount(
        decimal quantityPerHa,
        Material? materialDetail,
        decimal unitPrice,
        decimal effectiveTotalArea)
    {
        if (materialDetail == null || !materialDetail.AmmountPerMaterial.HasValue || unitPrice <= 0)
        {
            return 0M;
        }

        var pricePerHa = Math.Ceiling(quantityPerHa / materialDetail.AmmountPerMaterial.Value) * unitPrice;
        return pricePerHa * effectiveTotalArea;
    }
}