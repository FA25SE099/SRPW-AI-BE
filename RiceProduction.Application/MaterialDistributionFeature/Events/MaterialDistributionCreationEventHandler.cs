using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.CultivationFeature.Event;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.MaterialDistributionFeature.Events;

/// <summary>
/// Automatically creates material distribution records when a production plan is approved.
/// This handler listens to ProductionPlanApprovalEvent and creates distributions based on
/// the materials required in the production plan tasks.
/// </summary>
public class MaterialDistributionCreationEventHandler : INotificationHandler<ProductionPlanApprovalEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<MaterialDistributionCreationEventHandler> _logger;

    public MaterialDistributionCreationEventHandler(
        IApplicationDbContext context,
        ILogger<MaterialDistributionCreationEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ProductionPlanApprovalEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MaterialDistributionCreationEventHandler: Processing plan approval for Plan ID: {PlanId}",
            notification.PlanId);

        try
        {
            var plan = await FetchPlanWithRelationsAsync(notification.PlanId, cancellationToken);
            if (!IsPlanEligibleForDistribution(plan, notification.PlanId))
            {
                return;
            }

            var settings = await GetDistributionSettingsAsync(cancellationToken);
            var plotCultivations = GetPlotCultivationsForPlan(plan!);

            if (!plotCultivations.Any())
            {
                _logger.LogInformation(
                    "No plot cultivations found for plan {PlanId}. No material distributions will be created.",
                    notification.PlanId);
                return;
            }

            var distributions = await CreateMaterialDistributionsAsync(plan!, plotCultivations, settings, cancellationToken);

            if (distributions.Any())
            {
                _context.MaterialDistributions.AddRange(distributions);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully created {Count} material distributions for production plan {PlanId} (Group: {GroupId})",
                    distributions.Count, notification.PlanId, plan!.GroupId);
            }
            else
            {
                _logger.LogInformation(
                    "No material distributions created for plan {PlanId}. Either no materials in tasks or distributions already exist.",
                    notification.PlanId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating material distributions for production plan {PlanId}",
                notification.PlanId);
            // Don't throw - we don't want to break the approval process if distribution creation fails
        }
    }

    private bool IsPlanEligibleForDistribution(ProductionPlan? plan, Guid planId)
    {
        if (plan == null)
        {
            _logger.LogWarning("Production plan {PlanId} not found", planId);
            return false;
        }

        if (plan.Status != TaskStatus.Approved)
        {
            _logger.LogWarning(
                "Production plan {PlanId} is not in Approved status (current: {Status}). Skipping material distribution creation.",
                planId, plan.Status);
            return false;
        }

        if (plan.Group == null)
        {
            _logger.LogWarning("Production plan {PlanId} has no associated group", planId);
            return false;
        }

        if (plan.Group.Status != GroupStatus.Active)
        {
            _logger.LogWarning(
                "Group {GroupId} for plan {PlanId} is not Active (current: {Status}). Skipping material distribution creation.",
                plan.Group.Id, planId, plan.Group.Status);
            return false;
        }

        return true;
    }

    private async Task<ProductionPlan?> FetchPlanWithRelationsAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _context.ProductionPlans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Group)
                .ThenInclude(g => g!.YearSeason)
            .Include(p => p.Group)
                .ThenInclude(g => g!.GroupPlots)
                .ThenInclude(gp => gp.Plot)
                .ThenInclude(pl => pl.PlotCultivations)
            .Include(p => p.CurrentProductionStages)
                .ThenInclude(s => s.ProductionPlanTasks)
                .ThenInclude(t => t.ProductionPlanTaskMaterials)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    }

    private List<PlotCultivation> GetPlotCultivationsForPlan(ProductionPlan plan)
    {
        if (plan.Group?.YearSeason?.SeasonId == null)
        {
            _logger.LogWarning(
                "Group {GroupId} for plan {PlanId} has no season assigned",
                plan.GroupId, plan.Id);
            return new List<PlotCultivation>();
        }

        var seasonId = plan.Group.YearSeason.SeasonId;
        var plotCultivations = plan.Group.GroupPlots
            .Select(gp => gp.Plot)
            .SelectMany(pl => pl.PlotCultivations)
            .Where(pc => pc.SeasonId == seasonId)
            .GroupBy(pc => pc.PlotId)
            .Select(g => g.OrderByDescending(pc => pc.CreatedAt).First()) // Take latest per plot
            .ToList();

        _logger.LogInformation(
            "Found {Count} plot cultivations for plan {PlanId} in season {SeasonId}",
            plotCultivations.Count, plan.Id, seasonId);

        return plotCultivations;
    }

    private async Task<DistributionSettings> GetDistributionSettingsAsync(CancellationToken cancellationToken)
    {
        var settingKeys = new[]
        {
            "MaterialDistributionDaysBeforeTask",
            "SupervisorConfirmationWindowDays",
            "FarmerConfirmationWindowDays",
            "MaterialDistributionGracePeriodDays"
        };

        var settingsDict = await _context.SystemSettings
            .AsNoTracking()
            .Where(s => settingKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);

        return new DistributionSettings(
            DaysBeforeTask: ParseSettingInt(settingsDict, "MaterialDistributionDaysBeforeTask", 7),
            SupervisorConfirmationWindow: ParseSettingInt(settingsDict, "SupervisorConfirmationWindowDays", 2),
            FarmerConfirmationWindow: ParseSettingInt(settingsDict, "FarmerConfirmationWindowDays", 3),
            GracePeriod: ParseSettingInt(settingsDict, "MaterialDistributionGracePeriodDays", 1));
    }

    private int ParseSettingInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        if (settings.TryGetValue(key, out var value) && int.TryParse(value, out var result))
        {
            return result;
        }

        _logger.LogWarning(
            "Setting {Key} not found or invalid, using default value {Default}",
            key, defaultValue);
        return defaultValue;
    }

    private async Task<List<MaterialDistribution>> CreateMaterialDistributionsAsync(
        ProductionPlan plan,
        List<PlotCultivation> plotCultivations,
        DistributionSettings settings,
        CancellationToken cancellationToken)
    {
        var distributions = new List<MaterialDistribution>();

        var earliestTaskDate = plan.CurrentProductionStages
            .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
            .Where(t => t.ScheduledEndDate.HasValue)
            .Select(t => t.ScheduledEndDate!.Value)
            .OrderBy(d => d)
            .FirstOrDefault();

        if (earliestTaskDate == default)
        {
            _logger.LogWarning("No tasks with scheduled dates found in plan {PlanId}", plan.Id);
            return distributions;
        }

        var scheduledDate = earliestTaskDate.AddDays(-settings.DaysBeforeTask);
        var distributionDeadline = scheduledDate.AddDays(settings.GracePeriod);
        var supervisorDeadline = scheduledDate.AddDays(settings.SupervisorConfirmationWindow);

        var materialAggregations = AggregateRequiredMaterials(plan, plotCultivations);
        if (!materialAggregations.Any())
        {
            return distributions;
        }

        var plotCultivationIds = materialAggregations.Keys.Select(k => k.PlotCultivationId).Distinct().ToList();
        var existingKeys = (await _context.MaterialDistributions
            .AsNoTracking()
            .Where(md =>
                plotCultivationIds.Contains(md.PlotCultivationId) &&
                md.RelatedTaskId == null &&
                md.Status != DistributionStatus.Rejected)
            .Select(md => new { md.PlotCultivationId, md.MaterialId })
            .ToListAsync(cancellationToken))
            .Select(x => (x.PlotCultivationId, x.MaterialId))
            .ToHashSet();

        // Create one bulk distribution record per (PlotCultivation, Material) combination
        foreach (var ((plotCultivationId, materialId), totalQuantity) in materialAggregations)
        {
            if (existingKeys.Contains((plotCultivationId, materialId)))
            {
                _logger.LogDebug(
                    "Bulk distribution already exists for PlotCultivation {PlotCultivationId}, Material {MaterialId}",
                    plotCultivationId, materialId);
                continue;
            }

            distributions.Add(new MaterialDistribution
            {
                Id = Guid.NewGuid(),
                PlotCultivationId = plotCultivationId,
                MaterialId = materialId,
                RelatedTaskId = null, // NULL = bulk distribution for entire plan
                QuantityDistributed = totalQuantity,
                Status = DistributionStatus.Pending,
                ScheduledDistributionDate = scheduledDate,
                DistributionDeadline = distributionDeadline,
                SupervisorConfirmationDeadline = supervisorDeadline,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogDebug(
                "Created BULK distribution: PlotCultivation {PlotCultivationId}, Material {MaterialId}, Total Quantity {Quantity}",
                plotCultivationId, materialId, totalQuantity);
        }

        _logger.LogInformation(
            "Created {Count} bulk material distributions (aggregated across all tasks) for plan {PlanId}",
            distributions.Count, plan.Id);

        return distributions;
    }

    private static Dictionary<(Guid PlotCultivationId, Guid MaterialId), decimal> AggregateRequiredMaterials(
        ProductionPlan plan,
        List<PlotCultivation> plotCultivations)
    {
        var materialAggregations = new Dictionary<(Guid PlotCultivationId, Guid MaterialId), decimal>();
        var eligibleCultivations = plotCultivations.Where(pc => !(pc.Area <= 0)).ToList();

        var allTaskMaterials = plan.CurrentProductionStages
            .SelectMany(s => s.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
            .SelectMany(t => t.ProductionPlanTaskMaterials ?? Enumerable.Empty<ProductionPlanTaskMaterial>());

        foreach (var planTaskMaterial in allTaskMaterials)
        {
            foreach (var plotCultivation in eligibleCultivations)
            {
                var key = (plotCultivation.Id, planTaskMaterial.MaterialId);
                var quantityNeeded = planTaskMaterial.QuantityPerHa * (plotCultivation.Area ?? 0m);
                materialAggregations[key] = materialAggregations.GetValueOrDefault(key) + quantityNeeded;
            }
        }

        return materialAggregations;
    }

    private sealed record DistributionSettings(
        int DaysBeforeTask,
        int SupervisorConfirmationWindow,
        int FarmerConfirmationWindow,
        int GracePeriod);
}

