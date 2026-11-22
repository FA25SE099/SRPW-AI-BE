using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetTodayTask;

public class GetTodayTasksQueryHandler : IRequestHandler<GetTodayTasksQuery, Result<List<TodayTaskResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetTodayTasksQueryHandler> _logger;

    public GetTodayTasksQueryHandler(IApplicationDbContext context, ILogger<GetTodayTasksQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<TodayTaskResponse>>> Handle(GetTodayTasksQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date;

            var plotCultivations = await _context.PlotCultivations
                .AsNoTracking()
                .Where(pc => pc.Plot.FarmerId == request.FarmerId)
                .Include(pc => pc.CultivationVersions)
                .ToListAsync(cancellationToken);

            // Get the latest version (highest VersionOrder) for each PlotCultivation
            var latestVersionIds = plotCultivations
                .Select(pc => pc.CultivationVersions
                    .OrderByDescending(v => v.VersionOrder)
                    .FirstOrDefault()?.Id)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            var defaultOutstandingStatuses = new List<RiceProduction.Domain.Enums.TaskStatus>
            {
                RiceProduction.Domain.Enums.TaskStatus.Draft,
                RiceProduction.Domain.Enums.TaskStatus.InProgress,
                RiceProduction.Domain.Enums.TaskStatus.OnHold
            };

            var tasks = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct =>
                    ct.VersionId.HasValue && latestVersionIds.Contains(ct.VersionId.Value) &&
                    (!request.PlotCultivationId.HasValue || ct.PlotCultivationId == request.PlotCultivationId.Value) &&
                    (ct.PlotCultivation.Status == CultivationStatus.Planned || ct.PlotCultivation.Status == CultivationStatus.InProgress) &&
                    (request.StatusFilter.HasValue
                        ? ct.Status == request.StatusFilter.Value
                        : defaultOutstandingStatuses.Contains(ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft))))
#pragma warning disable CS8602 // Dereference of a possibly null reference
                .Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .Include(ct => ct.ProductionPlanTask)
                    .ThenInclude(ppt => ppt.ProductionStage)
                        .ThenInclude(ps => ps.ProductionPlan)
                .Include(ct => ct.CultivationTaskMaterials)
                    .ThenInclude(ctm => ctm.Material)
                .Include(ct => ct.AssignedVendor)
#pragma warning restore CS8602 // Dereference of a possibly null reference
                .OrderBy(ct => ct.ProductionPlanTask!.ScheduledDate)
                .ToListAsync(cancellationToken);

            // 4. Ánh xạ dữ liệu
            var responseData = tasks.Select(ct =>
            {
                var plot = ct.PlotCultivation.Plot;
                var plotArea = ct.PlotCultivation.Area.GetValueOrDefault(0M);
                
                var targetCompletionDate = ct.ScheduledEndDate.HasValue 
                    ? ct.ScheduledEndDate.Value.Date 
                    : ct.ProductionPlanTask!.ScheduledDate.Date;

                var isOverdue = targetCompletionDate < todayUtc;
                var currentStatus = ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft); 

                // Ánh xạ vật tư - Updated to match GetPlotCultivationByGroupAndPlotQueryHandler pattern
                var materialsResponse = ct.CultivationTaskMaterials
                    .Select(ctm => new TodayTaskMaterialResponse
                    {
                        MaterialId = ctm.MaterialId,
                        MaterialName = ctm.Material.Name,
                        MaterialUnit = ctm.Material.Unit,
                        
                        PlannedQuantityTotal = ctm.ActualQuantity,
                        
                        EstimatedAmount = ctm.ActualCost
                    })
                    .ToList();
                
                return new TodayTaskResponse
                {
                    CultivationTaskId = ct.Id,
                    PlotCultivationId = ct.PlotCultivationId,
                    TaskName = ct.CultivationTaskName ?? ct.ProductionPlanTask.TaskName,
                    Description = ct.Description ?? ct.ProductionPlanTask.Description,
                    TaskType = ct.TaskType.GetValueOrDefault(TaskType.Sowing), 
                    Status = currentStatus, 
                    
                    ScheduledDate = ct.ProductionPlanTask.ScheduledDate,
                    ScheduledEndDate = targetCompletionDate,
                    Priority = ct.ProductionPlanTask.Priority,
                    
                    IsOverdue = isOverdue,
                    IsUav = ct.AssignedToVendorId.HasValue, // True if UAV vendor is assigned
                    
                    PlotArea = plotArea,
                    PlotSoThuaSoTo = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                    
                    EstimatedMaterialCost = materialsResponse.Sum(m => m.EstimatedAmount),
                    Materials = materialsResponse
                };
            }).ToList();

            _logger.LogInformation("Successfully retrieved {Count} outstanding tasks for Farmer {FarmerId}.", responseData.Count, request.FarmerId);

            return Result<List<TodayTaskResponse>>.Success(responseData, "Successfully retrieved outstanding tasks.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving outstanding tasks for Farmer {FarmerId}", request.FarmerId);
            return Result<List<TodayTaskResponse>>.Failure("An error occurred while retrieving tasks.", "GetOutstandingTasksFailed");
        }
    }
}