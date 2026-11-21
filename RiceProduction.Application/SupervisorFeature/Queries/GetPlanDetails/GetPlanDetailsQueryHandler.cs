using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetPlanDetails;

public class GetPlanDetailsQueryHandler 
    : IRequestHandler<GetPlanDetailsQuery, Result<PlanDetailsResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlanDetailsQueryHandler> _logger;

    public GetPlanDetailsQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlanDetailsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PlanDetailsResponse>> Handle(
        GetPlanDetailsQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load plan with all relationships
            var plan = await _unitOfWork.Repository<ProductionPlan>().FindAsync(
                match: p => p.Id == request.ProductionPlanId,
                includeProperties: q => q
                    .Include(p => p.Group)
                        .ThenInclude(g => g!.YearSeason)
                    .Include(p => p.Group)
                        .ThenInclude(g => g!.GroupPlots)
                            .ThenInclude(gp => gp.Plot)
                                .ThenInclude(plot => plot.Farmer)
                    .Include(p => p.CurrentProductionStages.OrderBy(s => s.SequenceOrder))
                        .ThenInclude(s => s.ProductionPlanTasks.OrderBy(t => t.SequenceOrder))
                            .ThenInclude(t => t.ProductionPlanTaskMaterials)
                                .ThenInclude(m => m.Material)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.CultivationTasks)
                                .ThenInclude(ct => ct.PlotCultivation)
                                    .ThenInclude(pc => pc.Plot)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.CultivationTasks)
                                .ThenInclude(ct => ct.AssignedSupervisor)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.CultivationTasks)
                                .ThenInclude(ct => ct.AssignedVendor)
                    .Include(p => p.CurrentProductionStages)
                        .ThenInclude(s => s.ProductionPlanTasks)
                            .ThenInclude(t => t.CultivationTasks)
                                .ThenInclude(ct => ct.Verifier));

            if (plan == null)
            {
                return Result<PlanDetailsResponse>.Failure("Production plan not found");
            }

            // 2. Verify supervisor has access to this plan
            if (plan.Group?.SupervisorId != request.SupervisorId)
            {
                return Result<PlanDetailsResponse>.Failure(
                    "You do not have permission to view this production plan");
            }

            // 3. Get season info
            Season? season = null;
            if (plan.Group.YearSeason?.SeasonId != null)
            {
                season = await _unitOfWork.Repository<Season>()
                    .FindAsync(s => s.Id == plan.Group.YearSeason.SeasonId);
            }

            var metrics = AggregatePlanStages(plan.CurrentProductionStages);

            var plots = plan.Group?.GroupPlots?.Select(gp => gp.Plot).ToList() ?? new List<Plot>();
            var plotProgressList = await CalculatePlotProgress(plots);

            var response = new PlanDetailsResponse
            {
                ProductionPlanId = plan.Id,
                PlanName = plan.PlanName,
                Status = plan.Status.ToString(),
                BasePlantingDate = plan.BasePlantingDate,
                SubmittedAt = plan.SubmittedAt,
                ApprovedAt = plan.ApprovedAt,
                TotalArea = plan.TotalArea,

                GroupId = plan.GroupId ?? Guid.Empty,
                GroupName = $"Group {plan.GroupId.ToString()!.Substring(0, 8)}",
                SeasonName = season?.SeasonName ?? "Unknown",
                Year = plan.Group?.Year ?? DateTime.Now.Year,

                TotalStages = metrics.Stages.Count,
                CompletedStages = metrics.CompletedStages,
                InProgressStages = metrics.InProgressStages,
                TotalTasks = metrics.TotalTasks,
                CompletedTasks = metrics.CompletedTasks,
                InProgressTasks = metrics.InProgressTasks,
                PendingTasks = metrics.PendingTasks,
                OverallProgressPercentage = metrics.TotalTasks > 0
                    ? (metrics.CompletedTasks / (decimal)metrics.TotalTasks) * 100
                    : 0,

                DaysElapsed = (DateTime.Now - plan.BasePlantingDate).Days,
                EstimatedTotalDays = 0,
                IsOnSchedule = true,
                DaysBehindSchedule = null,
                EstimatedCompletionDate = null,

                EstimatedTotalCost = metrics.EstimatedTotalCost,
                ActualCostToDate = metrics.ActualCostToDate,
                RemainingEstimatedCost = metrics.EstimatedTotalCost - metrics.ActualCostToDate,
                CostVariance = metrics.ActualCostToDate - metrics.EstimatedTotalCost,
                CostVariancePercentage = metrics.EstimatedTotalCost > 0
                    ? ((metrics.ActualCostToDate - metrics.EstimatedTotalCost) / metrics.EstimatedTotalCost) * 100
                    : 0,

                ContingencyTasksCount = metrics.ContingencyTasks,
                TasksWithInterruptions = metrics.InterruptedTasks,
                HasActiveIssues = metrics.HasActiveIssues,

                Stages = metrics.Stages,
                PlotProgress = plotProgressList
            };

            _logger.LogInformation(
                "Retrieved detailed plan {PlanId} for supervisor {SupervisorId}",
                plan.Id, request.SupervisorId);

            return Result<PlanDetailsResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting plan details for plan {PlanId}",
                request.ProductionPlanId);
            return Result<PlanDetailsResponse>.Failure(
                $"Error retrieving plan details: {ex.Message}");
        }
    }

    private sealed class PlanStageMetrics
    {
        public List<StageDetails> Stages { get; } = new();
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int PendingTasks { get; set; }
        public int ContingencyTasks { get; set; }
        public int InterruptedTasks { get; set; }
        public int CompletedStages { get; set; }
        public int InProgressStages { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public decimal ActualCostToDate { get; set; }
        public bool HasActiveIssues { get; set; }
    }

    private static PlanStageMetrics AggregatePlanStages(IEnumerable<ProductionStage> stages)
    {
        var metrics = new PlanStageMetrics();

        foreach (var stage in stages.OrderBy(s => s.SequenceOrder))
        {
            var stageDetails = BuildStageDetails(stage, metrics);
            metrics.Stages.Add(stageDetails);
        }

        return metrics;
    }

    private static StageDetails BuildStageDetails(ProductionStage stage, PlanStageMetrics metrics)
    {
        var stageTasks = stage.ProductionPlanTasks.OrderBy(t => t.SequenceOrder).ToList();
        var taskDetailsList = new List<TaskDetails>(stageTasks.Count);

        int totalStageCultTasks = 0;
        int stageCompletedTasks = 0;
        int stageInProgressTasks = 0;
        int stagePendingTasks = 0;
        int stageContingencyTasks = 0;
        decimal stageEstimatedCost = 0;
        decimal stageActualCost = 0;
        DateTime? stageActualStart = null;
        DateTime? stageActualEnd = null;

        foreach (var task in stageTasks)
        {
            var cultivationTasks = task.CultivationTasks.ToList();

            int taskCompleted = cultivationTasks.Count(ct => ct.Status == TaskStatus.Completed);
            int taskInProgress = cultivationTasks.Count(ct => ct.Status == TaskStatus.InProgress);
            int taskPending = cultivationTasks.Count - taskCompleted - taskInProgress;
            var contingencyTask = cultivationTasks.FirstOrDefault(ct => ct.IsContingency);
            bool isContingency = contingencyTask != null;
            var interruptionReason = cultivationTasks
                .FirstOrDefault(ct => !string.IsNullOrEmpty(ct.InterruptionReason))?.InterruptionReason;

            totalStageCultTasks += cultivationTasks.Count;
            metrics.TotalTasks += cultivationTasks.Count;
            stageCompletedTasks += taskCompleted;
            stageInProgressTasks += taskInProgress;
            stagePendingTasks += taskPending;

            if (taskCompleted == cultivationTasks.Count && cultivationTasks.Any())
                metrics.CompletedTasks += cultivationTasks.Count;
            else if (taskInProgress > 0)
                metrics.InProgressTasks += taskInProgress;
            else
                metrics.PendingTasks += taskPending;

            if (isContingency)
            {
                metrics.ContingencyTasks++;
                stageContingencyTasks++;
            }

            if (!string.IsNullOrEmpty(interruptionReason))
            {
                metrics.InterruptedTasks++;
                metrics.HasActiveIssues = true;
            }

            decimal taskEstimated = task.EstimatedMaterialCost;
            decimal actualMaterialCost = cultivationTasks.Sum(ct => ct.ActualMaterialCost);
            decimal actualServiceCost = cultivationTasks.Sum(ct => ct.ActualServiceCost);
            decimal taskActual = actualMaterialCost + actualServiceCost;

            stageEstimatedCost += taskEstimated;
            stageActualCost += taskActual;
            metrics.EstimatedTotalCost += taskEstimated;
            metrics.ActualCostToDate += taskActual;

            var earliestStart = cultivationTasks
                .Where(ct => ct.ActualStartDate.HasValue)
                .Min(ct => ct.ActualStartDate);
            var latestEnd = cultivationTasks
                .Where(ct => ct.ActualEndDate.HasValue)
                .Max(ct => ct.ActualEndDate);

            if (earliestStart.HasValue && (!stageActualStart.HasValue || earliestStart < stageActualStart))
                stageActualStart = earliestStart;
            if (latestEnd.HasValue && (!stageActualEnd.HasValue || latestEnd > stageActualEnd))
                stageActualEnd = latestEnd;

            taskDetailsList.Add(BuildTaskDetails(
                task,
                cultivationTasks,
                taskCompleted,
                taskInProgress,
                isContingency,
                contingencyTask?.ContingencyReason,
                interruptionReason,
                earliestStart,
                latestEnd,
                taskEstimated,
                actualMaterialCost,
                actualServiceCost,
                taskActual));
        }

        string stageStatus;
        if (stageCompletedTasks == totalStageCultTasks && stageTasks.Any())
        {
            stageStatus = "Completed";
            metrics.CompletedStages++;
        }
        else if (stageInProgressTasks > 0 || stageCompletedTasks > 0)
        {
            stageStatus = "In Progress";
            metrics.InProgressStages++;
        }
        else
        {
            stageStatus = "Not Started";
        }

        return new StageDetails
        {
            StageId = stage.Id,
            StageName = stage.StageName,
            StageOrder = stage.SequenceOrder,
            Description = stage.Description ?? "",
            TotalTasks = totalStageCultTasks,
            CompletedTasks = stageCompletedTasks,
            InProgressTasks = stageInProgressTasks,
            PendingTasks = stagePendingTasks,
            ContingencyTasks = stageContingencyTasks,
            ProgressPercentage = totalStageCultTasks > 0
                ? (stageCompletedTasks / (decimal)totalStageCultTasks) * 100
                : 0,
            Status = stageStatus,
            ActualStartDate = stageActualStart,
            ActualEndDate = stageActualEnd,
            EstimatedStageCost = stageEstimatedCost,
            ActualStageCost = stageActualCost,
            CostVariance = stageActualCost - stageEstimatedCost,
            Tasks = taskDetailsList
        };
    }

    private static TaskDetails BuildTaskDetails(
        ProductionPlanTask task,
        List<CultivationTask> cultivationTasks,
        int taskCompleted,
        int taskInProgress,
        bool isContingency,
        string? contingencyReason,
        string? interruptionReason,
        DateTime? earliestStart,
        DateTime? latestEnd,
        decimal taskEstimated,
        decimal actualMaterialCost,
        decimal actualServiceCost,
        decimal taskActual)
    {
        var materials = task.ProductionPlanTaskMaterials.Select(m => new TaskMaterial
        {
            MaterialId = m.MaterialId,
            MaterialName = m.Material?.Name ?? "Unknown",
            MaterialType = m.Material?.Type.ToString() ?? "",
            QuantityPerHa = m.QuantityPerHa,
            Unit = m.Material?.Unit ?? "",
            EstimatedAmount = m.EstimatedAmount ?? 0,
            UnitPrice = 0,
            TotalCost = m.EstimatedAmount ?? 0
        }).ToList();

        var firstCultTask = cultivationTasks.FirstOrDefault();

        return new TaskDetails
        {
            TaskId = task.Id,
            TaskName = task.TaskName,
            TaskType = task.TaskType.ToString(),
            Description = task.Description ?? "",
            SequenceOrder = task.SequenceOrder,
            Status = task.Status.ToString(),
            Priority = task.Priority.ToString(),
            ScheduledDate = task.ScheduledDate,
            ScheduledEndDate = task.ScheduledEndDate,
            ActualStartDate = earliestStart,
            ActualEndDate = latestEnd,
            CompletedAt = cultivationTasks.Any(ct => ct.CompletedAt.HasValue)
                ? cultivationTasks.Max(ct => ct.CompletedAt)
                : null,
            DaysDelayed = CalculateDaysDelayed(task.ScheduledEndDate, latestEnd),
            IsContingency = isContingency,
            ContingencyReason = contingencyReason,
            InterruptionReason = interruptionReason,
            WeatherConditions = firstCultTask?.WeatherConditions,
            EstimatedCost = taskEstimated,
            ActualMaterialCost = actualMaterialCost,
            ActualServiceCost = actualServiceCost,
            TotalActualCost = taskActual,
            AssignedToUserId = firstCultTask?.AssignedToUserId,
            AssignedToName = firstCultTask?.AssignedSupervisor?.FullName,
            AssignedToVendorId = firstCultTask?.AssignedToVendorId,
            VendorName = firstCultTask?.AssignedVendor?.VendorName,
            VerifiedBy = firstCultTask?.VerifiedBy,
            VerifierName = firstCultTask?.Verifier?.FullName,
            VerifiedAt = firstCultTask?.VerifiedAt,
            Materials = materials,
            TotalExecutions = cultivationTasks.Count,
            CompletedExecutions = taskCompleted,
            InProgressExecutions = taskInProgress
        };
    }

    private static int? CalculateDaysDelayed(DateTime? scheduledEnd, DateTime? actualEnd)
    {
        if (!scheduledEnd.HasValue || !actualEnd.HasValue || actualEnd <= scheduledEnd)
            return null;

        return (actualEnd.Value - scheduledEnd.Value).Days;
    }

    private async Task<List<PlotProgressDetails>> CalculatePlotProgress(List<Plot> plots)
    {
        var plotProgressList = new List<PlotProgressDetails>();
        var plotIds = plots.Select(p => p.Id).ToList();

        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(
                pc => plotIds.Contains(pc.PlotId),
                includeProperties: q => q
                    .Include(pc => pc.Plot)
                        .ThenInclude(p => p.Farmer)
                    .Include(pc => pc.CultivationTasks)
                        .ThenInclude(ct => ct.ProductionPlanTask)
                            .ThenInclude(ppt => ppt.ProductionStage));

        var cultivationByPlotId = plotCultivations
            .GroupBy(pc => pc.PlotId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var plot in plots.OrderBy(p => p.SoThua).ThenBy(p => p.SoTo))
        {
            if (!cultivationByPlotId.TryGetValue(plot.Id, out var plotCult))
                continue;

            var cultivationTasks = plotCult.CultivationTasks.ToList();
            int totalTasks = cultivationTasks.Count;
            int completed = cultivationTasks.Count(ct => ct.Status == TaskStatus.Completed);
            int inProgress = cultivationTasks.Count(ct => ct.Status == TaskStatus.InProgress);
            int pending = totalTasks - completed - inProgress;
            int contingency = cultivationTasks.Count(ct => ct.IsContingency);

            decimal estimatedCost = cultivationTasks
                .Where(ct => ct.ProductionPlanTask != null)
                .Sum(ct => ct.ProductionPlanTask!.EstimatedMaterialCost);
            decimal actualCost = cultivationTasks
                .Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);

            var lastCompleted = cultivationTasks
                .Where(ct => ct.Status == TaskStatus.Completed && ct.CompletedAt.HasValue)
                .OrderByDescending(ct => ct.CompletedAt)
                .FirstOrDefault();

            var nextScheduled = cultivationTasks
                .Where(ct => ct.Status != TaskStatus.Completed && ct.ProductionPlanTask != null)
                .OrderBy(ct => ct.ProductionPlanTask!.ScheduledDate)
                .FirstOrDefault();

            var currentStage = cultivationTasks
                .Where(ct => ct.Status == TaskStatus.InProgress && ct.ProductionPlanTask != null)
                .Select(ct => ct.ProductionPlanTask!.ProductionStage)
                .FirstOrDefault();

            plotProgressList.Add(new PlotProgressDetails
            {
                PlotId = plot.Id,
                PlotIdentifier = $"SoThua {plot.SoThua}, SoTo {plot.SoTo}",
                Area = plot.Area,
                SoilType = plot.SoilType,
                FarmerId = plot.FarmerId,
                FarmerName = plot.Farmer?.FullName ?? "Unknown",
                FarmerPhone = plot.Farmer?.PhoneNumber,
                TotalTasks = totalTasks,
                CompletedTasks = completed,
                InProgressTasks = inProgress,
                PendingTasks = pending,
                ProgressPercentage = totalTasks > 0 ? (completed / (decimal)totalTasks) * 100 : 0,
                EstimatedCost = estimatedCost,
                ActualCost = actualCost,
                CostVariance = actualCost - estimatedCost,
                ContingencyCount = contingency,
                HasActiveIssues = cultivationTasks.Any(ct =>
                    ct.IsContingency || !string.IsNullOrEmpty(ct.InterruptionReason)),
                LatestCompletedTask = lastCompleted?.CultivationTaskName ?? lastCompleted?.ProductionPlanTask.TaskName,
                LatestCompletedAt = lastCompleted?.CompletedAt,
                NextScheduledTask = nextScheduled?.CultivationTaskName ?? nextScheduled?.ProductionPlanTask?.TaskName,
                NextScheduledDate = nextScheduled?.ProductionPlanTask?.ScheduledDate,
                CurrentStageName = currentStage?.StageName,
                CurrentStageOrder = currentStage?.SequenceOrder
            });
        }

        return plotProgressList;
    }
}

