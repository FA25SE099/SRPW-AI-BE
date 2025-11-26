using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq.Expressions;
using System.Linq;

namespace RiceProduction.Application.CultivationPlanFeature.Queries.GetTodayTask;

public class GetTodayTasksQueryHandler : IRequestHandler<GetTodayTasksQuery, Result<List<TodayTaskResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetTodayTasksQueryHandler> _logger;

    public GetTodayTasksQueryHandler(IUnitOfWork unitOfWork, ILogger<GetTodayTasksQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<TodayTaskResponse>>> Handle(GetTodayTasksQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date; // Ngày hôm nay (UTC Date only)
            
            // 1. Xác định Active Version IDs cho từng PlotCultivation (Logic remains the same)
            
            var plotCultivationIds = await _unitOfWork.Repository<PlotCultivation>()
                .ListAsync(filter: pc => pc.Plot.FarmerId == request.FarmerId); 
            
            var activeVersions = await _unitOfWork.Repository<CultivationVersion>()
                .ListAsync(
                    filter: v => plotCultivationIds.Select(pc => pc.Id).Contains(v.PlotCultivationId) && v.IsActive // Sử dụng Select(pc => pc.Id) để lấy IDs
                );
            
            var activeVersionIds = activeVersions.Select(v => v.Id).ToList();

            // Xác định các trạng thái tồn đọng mặc định
            var defaultOutstandingStatuses = new List<RiceProduction.Domain.Enums.TaskStatus> 
            { 
                RiceProduction.Domain.Enums.TaskStatus.Draft, 
                RiceProduction.Domain.Enums.TaskStatus.InProgress, 
                RiceProduction.Domain.Enums.TaskStatus.OnHold 
            };
            
            var statusesToFilter = request.StatusFilter.HasValue 
                ? new List<RiceProduction.Domain.Enums.TaskStatus> { request.StatusFilter.Value }
                : defaultOutstandingStatuses;

            var includesDraft = statusesToFilter.Contains(RiceProduction.Domain.Enums.TaskStatus.Draft);

            // 2. Xây dựng biểu thức lọc:
            Expression<Func<CultivationTask, bool>> filter = ct =>
                // Lọc theo Version đang hoạt động (MỚI)
                ct.VersionId.HasValue && activeVersionIds.Contains(ct.VersionId.Value) &&
                
                // Lọc theo PlotId (nếu được cung cấp)
                (!request.PlotId.HasValue || ct.PlotCultivation.PlotId == request.PlotId.Value) &&
                
                // Lọc theo Mùa vụ đang hoạt động
                (ct.PlotCultivation.Status == CultivationStatus.Planned || ct.PlotCultivation.Status == CultivationStatus.InProgress) &&
                
                //Lọc theo StatusFilter
                (request.StatusFilter.HasValue 
                    ? ct.Status == request.StatusFilter.Value 
                    : defaultOutstandingStatuses.Contains(ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft))); 
                    // &&

                // Điều kiện tồn đọng
                // (ct.ScheduledEndDate.HasValue && ct.ScheduledEndDate.Value.Date <= todayUtc || 
                //  !ct.ScheduledEndDate.HasValue && ct.ProductionPlanTask.ScheduledDate.Date <= todayUtc);

            // 3. Định nghĩa các Includes sâu
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderBy(ct => ct.ProductionPlanTask.ScheduledDate),
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation) 
                        .ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                            .ThenInclude(pptm => pptm.Material)
            );

            // 4. Ánh xạ dữ liệu
            var responseData = tasks.Select(ct =>
            {
                var plot = ct.PlotCultivation.Plot;
                var plotArea = ct.PlotCultivation.Area.GetValueOrDefault(0M);
                
                var targetCompletionDate = ct.ScheduledEndDate.HasValue 
                    ? ct.ScheduledEndDate.Value.Date 
                    : ct.ProductionPlanTask.ScheduledDate.Date;

                var isOverdue = targetCompletionDate < todayUtc;
                var currentStatus = ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft); 

                // Ánh xạ vật tư dự kiến
                var materialsResponse = ct.ProductionPlanTask.ProductionPlanTaskMaterials
                    .Select(pptm => new TodayTaskMaterialResponse
                    {
                        MaterialId = pptm.MaterialId,
                        MaterialName = pptm.Material.Name,
                        MaterialUnit = pptm.Material.Unit,
                        
                        PlannedQuantityTotal = pptm.QuantityPerHa * plotArea,
                        
                        EstimatedAmount = pptm.EstimatedAmount.GetValueOrDefault(0M) > 0 
                            ? (pptm.EstimatedAmount.GetValueOrDefault(0M) * plotArea / plot.Area) 
                            : 0M
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
                    Priority = ct.ProductionPlanTask.Priority,
                    
                    IsOverdue = isOverdue,
                    
                    PlotArea = plotArea,
                    PlotSoThuaSoTo = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                    
                    EstimatedMaterialCost = ct.ProductionPlanTask.EstimatedMaterialCost,
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