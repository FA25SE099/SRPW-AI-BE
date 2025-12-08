using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
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
            if (plan.Group.SeasonId.HasValue)
            {
                season = await _unitOfWork.Repository<Season>()
                    .FindAsync(s => s.Id == plan.Group.SeasonId.Value);
            }

            // 4. Calculate detailed progress
            var stageDetailsList = new List<StageDetails>();
            int totalTasks = 0;
            int completedTasks = 0;
            int inProgressTasks = 0;
            int pendingTasks = 0;
            int contingencyTasks = 0;
            int interruptedTasks = 0;
            int completedStages = 0;
            int inProgressStages = 0;
            
            decimal estimatedTotalCost = 0;
            decimal actualCostToDate = 0;
            bool hasActiveIssues = false;

            foreach (var stage in plan.CurrentProductionStages.OrderBy(s => s.SequenceOrder))
            {
                var stageTasks = stage.ProductionPlanTasks.OrderBy(t => t.SequenceOrder).ToList();
                var taskDetailsList = new List<TaskDetails>();
                
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
                    bool isContingency = cultivationTasks.Any(ct => ct.IsContingency);
                    
                    totalTasks += cultivationTasks.Count;
                    stageCompletedTasks += taskCompleted;
                    stageInProgressTasks += taskInProgress;
                    stagePendingTasks += taskPending;
                    
                    if (taskCompleted == cultivationTasks.Count && cultivationTasks.Any())
                        completedTasks += cultivationTasks.Count;
                    else if (taskInProgress > 0)
                        inProgressTasks += taskInProgress;
                    else
                        pendingTasks += taskPending;
                    
                    if (isContingency)
                    {
                        contingencyTasks++;
                        stageContingencyTasks++;
                    }
                    
                    if (cultivationTasks.Any(ct => !string.IsNullOrEmpty(ct.InterruptionReason)))
                    {
                        interruptedTasks++;
                        hasActiveIssues = true;
                    }
                    
                    // Costs
                    decimal taskEstimated = task.EstimatedMaterialCost;
                    decimal taskActual = cultivationTasks.Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);
                    
                    stageEstimatedCost += taskEstimated;
                    stageActualCost += taskActual;
                    estimatedTotalCost += taskEstimated;
                    actualCostToDate += taskActual;
                    
                    // Track stage timing
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
                    
                    var materials = task.ProductionPlanTaskMaterials.Select(m => new TaskMaterial
                    {
                        MaterialId = m.MaterialId,
                        MaterialName = m.Material?.Name ?? "Unknown",
                        MaterialType = m.Material?.Type.ToString() ?? "",
                        QuantityPerHa = m.QuantityPerHa,
                        Unit = m.Material?.Unit ?? "",
                        EstimatedAmount = m.EstimatedAmount ?? 0,
                        UnitPrice =  0,
                        TotalCost = (m.EstimatedAmount ?? 0) 
                    }).ToList();
                    
                    // Get assignment info
                    var firstCultTask = cultivationTasks.FirstOrDefault();
                    
                    taskDetailsList.Add(new TaskDetails
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
                        ContingencyReason = cultivationTasks.FirstOrDefault(ct => ct.IsContingency)?.ContingencyReason,
                        InterruptionReason = cultivationTasks.FirstOrDefault(ct => !string.IsNullOrEmpty(ct.InterruptionReason))?.InterruptionReason,
                        WeatherConditions = firstCultTask?.WeatherConditions,
                        EstimatedCost = taskEstimated,
                        ActualMaterialCost = cultivationTasks.Sum(ct => ct.ActualMaterialCost),
                        ActualServiceCost = cultivationTasks.Sum(ct => ct.ActualServiceCost),
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
                    });
                }
                
                // Determine stage status
                string stageStatus;
                if (stageCompletedTasks == stageTasks.Sum(t => t.CultivationTasks.Count) && stageTasks.Any())
                {
                    stageStatus = "Completed";
                    completedStages++;
                }
                else if (stageInProgressTasks > 0 || stageCompletedTasks > 0)
                {
                    stageStatus = "In Progress";
                    inProgressStages++;
                }
                else
                {
                    stageStatus = "Not Started";
                }
                
                //bool isDelayed = stageActualEnd.HasValue && stageActualEnd > stage.EndDate;
                //int? daysDelayed = isDelayed 
                //    ? (int?)(stageActualEnd!.Value - stage.EndDate).Days 
                //    : null;
                
                int totalStageCultTasks = stageTasks.Sum(t => t.CultivationTasks.Count);
                
                stageDetailsList.Add(new StageDetails
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
                });
            }

            var daysElapsed = (DateTime.Now - plan.BasePlantingDate).Days;
            //var estimatedTotalDays = plan.CurrentProductionStages.Any() 
            //    ? (int)(plan.CurrentProductionStages.Max(s => s.EndDate) - plan.BasePlantingDate).TotalDays 
            //    : 0;

            var estimatedTotalDays = 0;
            bool isOnSchedule = true;
            int? daysBehindSchedule = null;
            DateTime? estimatedCompletion = null;
            
            if (estimatedTotalDays > 0 && totalTasks > 0)
            {
                decimal expectedProgress = daysElapsed > estimatedTotalDays 
                    ? 100 
                    : ((decimal)daysElapsed / estimatedTotalDays) * 100;
                decimal actualProgress = ((decimal)completedTasks / totalTasks) * 100;
                
                if (actualProgress < expectedProgress - 10)
                {
                    isOnSchedule = false;
                    daysBehindSchedule = (int)((expectedProgress - actualProgress) / 100 * estimatedTotalDays);
                }
                
                // Estimate completion based on current rate
                if (completedTasks > 0 && daysElapsed > 0)
                {
                    double daysPerTask = (double)daysElapsed / completedTasks;
                    int remainingTasks = totalTasks - completedTasks;
                    int estimatedRemainingDays = (int)(remainingTasks * daysPerTask);
                    estimatedCompletion = DateTime.Now.AddDays(estimatedRemainingDays);
                }
            }

            // 6. Calculate plot-level progress
            var plots = plan.Group?.GroupPlots?.Select(gp => gp.Plot).ToList() ?? new List<Plot>();
            var plotProgressList = await CalculatePlotProgress(plan, plots);

            // 7. Build response
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
                
                TotalStages = stageDetailsList.Count,
                CompletedStages = completedStages,
                InProgressStages = inProgressStages,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                PendingTasks = pendingTasks,
                OverallProgressPercentage = totalTasks > 0 ? (completedTasks / (decimal)totalTasks) * 100 : 0,
                
                DaysElapsed = daysElapsed,
                EstimatedTotalDays = estimatedTotalDays,
                IsOnSchedule = isOnSchedule,
                DaysBehindSchedule = daysBehindSchedule,
                EstimatedCompletionDate = estimatedCompletion,
                
                EstimatedTotalCost = estimatedTotalCost,
                ActualCostToDate = actualCostToDate,
                RemainingEstimatedCost = estimatedTotalCost - actualCostToDate,
                CostVariance = actualCostToDate - estimatedTotalCost,
                CostVariancePercentage = estimatedTotalCost > 0 
                    ? ((actualCostToDate - estimatedTotalCost) / estimatedTotalCost) * 100 
                    : 0,
                
                ContingencyTasksCount = contingencyTasks,
                TasksWithInterruptions = interruptedTasks,
                HasActiveIssues = hasActiveIssues,
                
                Stages = stageDetailsList,
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

    private int? CalculateDaysDelayed(DateTime? scheduledEnd, DateTime? actualEnd)
    {
        if (!scheduledEnd.HasValue || !actualEnd.HasValue)
            return null;
        
        if (actualEnd > scheduledEnd)
            return (int)(actualEnd.Value - scheduledEnd.Value).Days;
        
        return null;
    }

    private async Task<List<PlotProgressDetails>> CalculatePlotProgress(
        ProductionPlan plan, 
        List<Plot> plots)
    {
        var plotProgressList = new List<PlotProgressDetails>();
        
        // Get all plot cultivations for this group
        var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
            .ListAsync(
                pc => plots.Select(p => p.Id).Contains(pc.PlotId),
                includeProperties: q => q
                    .Include(pc => pc.Plot)
                        .ThenInclude(p => p.Farmer)
                    .Include(pc => pc.CultivationTasks)
                        .ThenInclude(ct => ct.ProductionPlanTask)
                            .ThenInclude(ppt => ppt.ProductionStage));
        
        foreach (var plot in plots.OrderBy(p => p.SoThua).ThenBy(p => p.SoTo))
        {
            var plotCult = plotCultivations.FirstOrDefault(pc => pc.PlotId == plot.Id);
            if (plotCult == null) continue;
            
            var cultivationTasks = plotCult.CultivationTasks.ToList();
            int totalTasks = cultivationTasks.Count;
            int completed = cultivationTasks.Count(ct => ct.Status == TaskStatus.Completed);
            int inProgress = cultivationTasks.Count(ct => ct.Status == TaskStatus.InProgress);
            int pending = totalTasks - completed - inProgress;
            int contingency = cultivationTasks.Count(ct => ct.IsContingency);
            
            decimal estimatedCost = cultivationTasks
                .Select(ct => ct.ProductionPlanTask.EstimatedMaterialCost)
                .Sum();
            decimal actualCost = cultivationTasks
                .Sum(ct => ct.ActualMaterialCost + ct.ActualServiceCost);
            
            var lastCompleted = cultivationTasks
                .Where(ct => ct.Status == TaskStatus.Completed && ct.CompletedAt.HasValue)
                .OrderByDescending(ct => ct.CompletedAt)
                .FirstOrDefault();
            
            var nextScheduled = cultivationTasks
                .Where(ct => ct.Status != TaskStatus.Completed)
                .OrderBy(ct => ct.ProductionPlanTask.ScheduledDate)
                .FirstOrDefault();
            
            var currentStage = cultivationTasks
                .Where(ct => ct.Status == TaskStatus.InProgress)
                .Select(ct => ct.ProductionPlanTask.ProductionStage)
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
                NextScheduledTask = nextScheduled?.CultivationTaskName ?? nextScheduled?.ProductionPlanTask.TaskName,
                NextScheduledDate = nextScheduled?.ProductionPlanTask.ScheduledDate,
                CurrentStageName = currentStage?.StageName,
                CurrentStageOrder = currentStage?.SequenceOrder
            });
        }
        
        return plotProgressList;
    }
}

