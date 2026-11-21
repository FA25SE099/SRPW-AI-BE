using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;

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
            var group = await _unitOfWork.Repository<Group>().FindAsync(g => g.Id == request.GroupId);

            if (group == null)
            {
                return Result<GeneratePlanDraftResponse>.Failure($"Group with ID {request.GroupId} not found.", "GroupNotFound");
            }

            if (group.TotalArea == null || group.TotalArea.Value <= 0)
            {
                return Result<GeneratePlanDraftResponse>.Failure("Group's Total Area is not defined or is zero.", "GroupAreaMissing");
            }

            var effectiveTotalArea = group.TotalArea.Value;

            var standardPlan = await _unitOfWork.Repository<StandardPlan>().FindAsync(
                match: sp => sp.Id == request.StandardPlanId,
                includeProperties: q => q
                    .Include(sp => sp.StandardPlanStages.OrderBy(s => s.SequenceOrder))
                        .ThenInclude(sps => sps.StandardPlanTasks.OrderBy(t => t.SequenceOrder))
                            .ThenInclude(spt => spt.StandardPlanTaskMaterials)
                                .ThenInclude(sptm => sptm.Material));

            if (standardPlan == null)
            {
                return Result<GeneratePlanDraftResponse>.Failure($"Standard Plan with ID {request.StandardPlanId} not found.", "StandardPlanNotFound");
            }

            var allStandardMaterials = standardPlan.StandardPlanStages
                .SelectMany(s => s.StandardPlanTasks)
                .SelectMany(t => t.StandardPlanTaskMaterials)
                .ToList();

            var materialIds = allStandardMaterials
                .Select(m => m.MaterialId)
                .Distinct()
                .ToList();

            var priceReferenceDate = DateTime.SpecifyKind(request.BasePlantingDate.Date, DateTimeKind.Utc);

            var potentialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => materialIds.Contains(p.MaterialId) && p.ValidFrom.Date <= priceReferenceDate);

            var materialPriceMap = potentialPrices
                .GroupBy(p => p.MaterialId)
                .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
                .ToDictionary(p => p.MaterialId, p => new { p.PricePerMaterial, p.ValidFrom });

            var materialDetailsMap = allStandardMaterials
                .Select(m => m.Material)
                .Distinct()
                .ToDictionary(m => m.Id, m => m);

            decimal totalPlanCost = 0M;
            var priceWarnings = new List<string>();
            var now = DateTime.UtcNow;
            var stagesResponse = new List<ProductionStageResponse>();

            foreach (var standardStage in standardPlan.StandardPlanStages)
            {
                var tasksResponse = new List<ProductionPlanTaskResponse>();

                foreach (var standardTask in standardStage.StandardPlanTasks.OrderBy(t => t.DaysAfter))
                {
                    decimal taskTotalEstimatedCost = 0M;
                    var scheduledDate = request.BasePlantingDate.AddDays(standardTask.DaysAfter);
                    var materialsResponse = new List<ProductionPlanTaskMaterialResponse>();

                    foreach (var standardMaterial in standardTask.StandardPlanTaskMaterials)
                    {
                        if (!materialDetailsMap.TryGetValue(standardMaterial.MaterialId, out var materialDetail))
                        {
                            _logger.LogWarning("Material details not found for Material ID {MId}.", standardMaterial.MaterialId);
                            continue;
                        }

                        var priceInfo = materialPriceMap.GetValueOrDefault(standardMaterial.MaterialId);
                        var unitPrice = priceInfo?.PricePerMaterial ?? 0M;
                        var priceValidFrom = priceInfo?.ValidFrom;

                        var materialPriceWarning = EvaluatePriceWarning(
                            materialDetail, unitPrice, priceValidFrom,
                            priceReferenceDate, request.BasePlantingDate, now, priceWarnings);

                        var amountPerUnit = materialDetail.AmmountPerMaterial.GetValueOrDefault(1M);
                        var pricePerHa = Math.Ceiling(standardMaterial.QuantityPerHa / amountPerUnit) * unitPrice;
                        var estimatedAmount = pricePerHa * effectiveTotalArea;

                        taskTotalEstimatedCost += estimatedAmount;
                        totalPlanCost += estimatedAmount;

                        materialsResponse.Add(new ProductionPlanTaskMaterialResponse
                        {
                            MaterialId = standardMaterial.MaterialId,
                            MaterialName = materialDetail.Name,
                            MaterialUnit = materialDetail.Unit,
                            QuantityPerHa = standardMaterial.QuantityPerHa,
                            EstimatedAmount = estimatedAmount,
                            UnitPrice = unitPrice > 0 ? unitPrice : null,
                            PriceValidFrom = priceValidFrom,
                            PriceWarning = materialPriceWarning
                        });
                    }

                    tasksResponse.Add(new ProductionPlanTaskResponse
                    {
                        TaskName = standardTask.TaskName,
                        Description = standardTask.Description,
                        TaskType = standardTask.TaskType,
                        Priority = standardTask.Priority,
                        SequenceOrder = standardTask.SequenceOrder,
                        ScheduledDate = scheduledDate,
                        ScheduledEndDate = scheduledDate.AddDays(standardTask.DurationDays - 1),
                        EstimatedMaterialCost = taskTotalEstimatedCost,
                        Materials = materialsResponse
                    });
                }

                stagesResponse.Add(new ProductionStageResponse
                {
                    StageName = standardPlan.PlanName,
                    SequenceOrder = standardStage.SequenceOrder,
                    Description = standardStage.Notes,
                    TypicalDurationDays = standardStage.ExpectedDurationDays,
                    Tasks = tasksResponse
                });
            }

            var response = new GeneratePlanDraftResponse
            {
                StandardPlanId = standardPlan.Id,
                GroupId = request.GroupId,
                PlanName = $"{standardPlan.PlanName} - {group.Id.ToString().Substring(0, 8)}",
                TotalArea = effectiveTotalArea,
                BasePlantingDate = request.BasePlantingDate,
                Stages = stagesResponse,
                EstimatedTotalPlanCost = totalPlanCost,
                PriceWarnings = priceWarnings
            };

            var warningMessage = priceWarnings.Any()
                ? $"Successfully generated production plan draft with {priceWarnings.Count} price warning(s)."
                : "Successfully generated production plan draft.";

            _logger.LogInformation("Successfully generated draft ProductionPlan from StandardPlan ID {SPId} for Group ID {GId}. Total Cost: {Cost}, Warnings: {WarningCount}",
                request.StandardPlanId, request.GroupId, totalPlanCost, priceWarnings.Count);

            return Result<GeneratePlanDraftResponse>.Success(response, warningMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating production plan draft from StandardPlan ID {SPId} for Group ID {GId}.", request.StandardPlanId, request.GroupId);

            return Result<GeneratePlanDraftResponse>.Failure("An error occurred while generating the plan draft.", "GeneratePlanDraft failed.");
        }
    }

    private string? EvaluatePriceWarning(
        Material materialDetail,
        decimal unitPrice,
        DateTime? priceValidFrom,
        DateTime priceReferenceDate,
        DateTime basePlantingDate,
        DateTime now,
        List<string> priceWarnings)
    {
        if (unitPrice == 0M || !priceValidFrom.HasValue)
        {
            priceWarnings.Add($" {materialDetail.Name}: No price data available for planting date {basePlantingDate:yyyy-MM-dd}");
            _logger.LogWarning("Price not found for Material ID {MId} on date {Date}.", materialDetail.Id, priceReferenceDate.ToShortDateString());
            return $"No price available for '{materialDetail.Name}'";
        }

        var priceAge = (now - priceValidFrom.Value).Days;
        if (priceAge > 90)
        {
            priceWarnings.Add($" {materialDetail.Name}: Price is outdated ({priceAge} days old, last updated {priceValidFrom.Value:yyyy-MM-dd})");
            _logger.LogWarning("Price for Material '{Material}' is {Days} days old", materialDetail.Name, priceAge);
            return $"Price is {priceAge} days old (from {priceValidFrom.Value:yyyy-MM-dd})";
        }

        if (priceValidFrom.Value > priceReferenceDate)
        {
            priceWarnings.Add($" {materialDetail.Name}: Price valid from date is in the future");
            return $"Price date is in the future ({priceValidFrom.Value:yyyy-MM-dd})";
        }

        return null;
    }
}
