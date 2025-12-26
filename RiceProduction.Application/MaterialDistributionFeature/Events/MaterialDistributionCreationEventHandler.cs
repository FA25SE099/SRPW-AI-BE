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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MaterialDistributionCreationEventHandler> _logger;

    public MaterialDistributionCreationEventHandler(
        IUnitOfWork unitOfWork,
        ILogger<MaterialDistributionCreationEventHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(ProductionPlanApprovalEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MaterialDistributionCreationEventHandler: Processing plan approval for Plan ID: {PlanId}",
            notification.PlanId);

        try
        {
            // Fetch the production plan with all necessary relationships
            var plan = await FetchPlanWithRelationsAsync(notification.PlanId, cancellationToken);

            if (plan == null)
            {
                _logger.LogWarning("Production plan {PlanId} not found", notification.PlanId);
                return;
            }

            if (plan.Status != TaskStatus.Approved)
            {
                _logger.LogWarning(
                    "Production plan {PlanId} is not in Approved status (current: {Status}). Skipping material distribution creation.",
                    notification.PlanId, plan.Status);
                return;
            }

            if (plan.Group == null)
            {
                _logger.LogWarning("Production plan {PlanId} has no associated group", notification.PlanId);
                return;
            }

            if (plan.Group.Status != GroupStatus.Active)
            {
                _logger.LogWarning(
                    "Group {GroupId} for plan {PlanId} is not Active (current: {Status}). Skipping material distribution creation.",
                    plan.Group.Id, notification.PlanId, plan.Group.Status);
                return;
            }

            // Get system settings for time window calculations
            var settings = await GetDistributionSettingsAsync(cancellationToken);

            // Get all plot cultivations for the group's season
            var plotCultivations = await GetPlotCultivationsForPlanAsync(plan, cancellationToken);

            if (!plotCultivations.Any())
            {
                _logger.LogInformation(
                    "No plot cultivations found for plan {PlanId}. No material distributions will be created.",
                    notification.PlanId);
                return;
            }

            // Create material distributions
            var distributions = await CreateMaterialDistributionsAsync(
                plan, plotCultivations, settings, cancellationToken);

            if (distributions.Any())
            {
                await _unitOfWork.Repository<MaterialDistribution>().AddRangeAsync(distributions);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully created {Count} material distributions for production plan {PlanId} (Group: {GroupId})",
                    distributions.Count, notification.PlanId, plan.GroupId);
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

    private async Task<ProductionPlan?> FetchPlanWithRelationsAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plans = await _unitOfWork.Repository<ProductionPlan>().ListAsync(
            filter: p => p.Id == planId,
            includeProperties: q => q
                .Include(p => p.Group)
                    .ThenInclude(g => g!.YearSeason)
                .Include(p => p.Group)
                    .ThenInclude(g => g!.GroupPlots)
                    .ThenInclude(gp => gp.Plot)
                    .ThenInclude(pl => pl.PlotCultivations)
                .Include(p => p.CurrentProductionStages)
                    .ThenInclude(s => s.ProductionPlanTasks)
                    .ThenInclude(t => t.ProductionPlanTaskMaterials)
        );

        return plans.FirstOrDefault();
    }

    private Task<List<PlotCultivation>> GetPlotCultivationsForPlanAsync(
        ProductionPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Group?.YearSeason?.SeasonId == null)
        {
            _logger.LogWarning(
                "Group {GroupId} for plan {PlanId} has no season assigned",
                plan.GroupId, plan.Id);
            return Task.FromResult(new List<PlotCultivation>());
        }

        var seasonId = plan.Group.YearSeason.SeasonId;
        var plots = plan.Group.GroupPlots.Select(gp => gp.Plot).ToList();

        // Get only plot cultivations for the current season
        var plotCultivations = plots
            .SelectMany(pl => pl.PlotCultivations)
            .Where(pc => pc.SeasonId == seasonId)
            .GroupBy(pc => pc.PlotId)
            .Select(g => g.OrderByDescending(pc => pc.CreatedAt).First()) // Take latest per plot
            .ToList();

        _logger.LogInformation(
            "Found {Count} plot cultivations for plan {PlanId} in season {SeasonId}",
            plotCultivations.Count, plan.Id, seasonId);

        return Task.FromResult(plotCultivations);
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

        var settings = await _unitOfWork.Repository<SystemSetting>()
            .ListAsync(s => settingKeys.Contains(s.SettingKey));

        var settingsDict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);

        return new DistributionSettings
        {
            DaysBeforeTask = ParseSettingInt(settingsDict, "MaterialDistributionDaysBeforeTask", 7),
            SupervisorConfirmationWindow = ParseSettingInt(settingsDict, "SupervisorConfirmationWindowDays", 2),
            FarmerConfirmationWindow = ParseSettingInt(settingsDict, "FarmerConfirmationWindowDays", 3),
            GracePeriod = ParseSettingInt(settingsDict, "MaterialDistributionGracePeriodDays", 1)
        };
    }

    private int ParseSettingInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        if (settings.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;

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

        // Find the earliest task date for scheduling distribution
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

        // Calculate distribution dates based on earliest task
        var scheduledDate = earliestTaskDate.AddDays(-settings.DaysBeforeTask);
        var distributionDeadline = scheduledDate.AddDays(settings.GracePeriod);
        var supervisorDeadline = scheduledDate.AddDays(settings.SupervisorConfirmationWindow);

        // Aggregate materials across all tasks - group by (PlotCultivation, Material)
        var materialAggregations = new Dictionary<(Guid PlotCultivationId, Guid MaterialId), decimal>();

        foreach (var stage in plan.CurrentProductionStages)
        {
            foreach (var planTask in stage.ProductionPlanTasks ?? Enumerable.Empty<ProductionPlanTask>())
            {
                if (planTask.ProductionPlanTaskMaterials == null || !planTask.ProductionPlanTaskMaterials.Any())
                    continue;

                foreach (var plotCultivation in plotCultivations)
                {
                    if (plotCultivation.Area <= 0)
                        continue;

                    foreach (var planTaskMaterial in planTask.ProductionPlanTaskMaterials)
                    {
                        var key = (plotCultivation.Id, planTaskMaterial.MaterialId);
                        var quantityNeeded = planTaskMaterial.QuantityPerHa * (plotCultivation.Area ?? 0m);

                        if (materialAggregations.ContainsKey(key))
                        {
                            materialAggregations[key] += quantityNeeded;
                        }
                        else
                        {
                            materialAggregations[key] = quantityNeeded;
                        }
                    }
                }
            }
        }

        // Create ONE distribution record per (PlotCultivation, Material) combination
        foreach (var aggregation in materialAggregations)
        {
            var (plotCultivationId, materialId) = aggregation.Key;
            var totalQuantity = aggregation.Value;

            // Check if distribution already exists (prevent duplicates)
            var existingDistribution = await _unitOfWork.Repository<MaterialDistribution>()
                .FindAsync(md =>
                    md.PlotCultivationId == plotCultivationId &&
                    md.MaterialId == materialId &&
                    md.RelatedTaskId == null && // Bulk distribution has no specific task
                    md.Status != DistributionStatus.Rejected);

            if (existingDistribution != null)
            {
                _logger.LogDebug(
                    "Bulk distribution already exists for PlotCultivation {PlotCultivationId}, Material {MaterialId}",
                    plotCultivationId, materialId);
                continue;
            }

            var distribution = new MaterialDistribution
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
            };

            distributions.Add(distribution);

            _logger.LogDebug(
                "Created BULK distribution: PlotCultivation {PlotCultivationId}, Material {MaterialId}, Total Quantity {Quantity}",
                plotCultivationId, materialId, totalQuantity);
        }

        _logger.LogInformation(
            "Created {Count} bulk material distributions (aggregated across all tasks) for plan {PlanId}",
            distributions.Count, plan.Id);

        return distributions;
    }

    private class DistributionSettings
    {
        public int DaysBeforeTask { get; set; }
        public int SupervisorConfirmationWindow { get; set; }
        public int FarmerConfirmationWindow { get; set; }
        public int GracePeriod { get; set; }
    }
}

