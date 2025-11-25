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
        try{
            var todayUtc = DateTime.UtcNow.Date;

            // Xác định các trạng thái tồn đọng mặc định
            var defaultOutstandingStatuses = new List<RiceProduction.Domain.Enums.TaskStatus> 
            { 
                RiceProduction.Domain.Enums.TaskStatus.Draft, 
                RiceProduction.Domain.Enums.TaskStatus.InProgress, 
                RiceProduction.Domain.Enums.TaskStatus.OnHold,
                RiceProduction.Domain.Enums.TaskStatus.Completed 
            };
            
            var statusesToFilter = request.StatusFilter.HasValue 
                ? new List<RiceProduction.Domain.Enums.TaskStatus> { request.StatusFilter.Value } // Chỉ lọc trạng thái đơn nếu được cung cấp
                : defaultOutstandingStatuses; // Ngược lại, dùng các trạng thái tồn đọng mặc định

            // Kiểm tra xem trạng thái Draft có nằm trong bộ lọc không
            var includesDraft = statusesToFilter.Contains(RiceProduction.Domain.Enums.TaskStatus.Draft);

            // 1. Xây dựng biểu thức lọc: Lấy CultivationTasks chưa hoàn thành (Outstanding)
            Expression<Func<CultivationTask, bool>> filter = ct =>
                ct.PlotCultivation.Plot.FarmerId == request.FarmerId &&
                
                // --- Lọc theo PlotId (Tùy chọn) ---
                (!request.PlotId.HasValue || ct.PlotCultivation.PlotId == request.PlotId.Value) &&
                
                // --- Lọc theo Mùa vụ Hiện tại ---
                (ct.PlotCultivation.Status == CultivationStatus.Planned || ct.PlotCultivation.Status == CultivationStatus.InProgress) &&
                
                // --- Lọc theo StatusFilter (FIXED: Xử lý an toàn cho Nullable Enum) ---
                (
                    // Điều kiện 1: Trạng thái có giá trị và nằm trong danh sách lọc
                    (ct.Status.HasValue && statusesToFilter.Contains(ct.Status.Value)) ||
                    
                    // Điều kiện 2: Trạng thái là NULL VÀ Draft nằm trong danh sách lọc
                    (!ct.Status.HasValue && includesDraft) 
                );
                    // &&

                // Điều kiện tồn đọng: Task có lịch trình kết thúc hoặc bắt đầu vào hoặc trước ngày hôm nay.
                // (ct.ScheduledEndDate.HasValue && ct.ScheduledEndDate.Value.Date <= todayUtc || 
                //  !ct.ScheduledEndDate.HasValue && ct.ProductionPlanTask.ScheduledDate.Date <= todayUtc);

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

            // 3. Ánh xạ dữ liệu
            var responseData = tasks.Select(ct =>
            {
                var plot = ct.PlotCultivation.Plot;
                var plotArea = ct.PlotCultivation.Area.GetValueOrDefault(0M);
                
                var targetCompletionDate = ct.ScheduledEndDate.HasValue 
                    ? ct.ScheduledEndDate.Value.Date 
                    : ct.ProductionPlanTask.ScheduledDate.Date;

                var isOverdue = targetCompletionDate < todayUtc;

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
                    TaskType = ct.TaskType.GetValueOrDefault(RiceProduction.Domain.Enums.TaskType.Harvesting),
                    Status = ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft),

                    ScheduledDate = ct.ProductionPlanTask.ScheduledDate,
                    Priority = ct.ProductionPlanTask.Priority,

                    IsOverdue = ct.Status.GetValueOrDefault(RiceProduction.Domain.Enums.TaskStatus.Draft) == RiceProduction.Domain.Enums.TaskStatus.InProgress ? isOverdue : false, // <-- Gán trạng thái quá hạn

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