using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query; // Cần thiết cho Includes

namespace RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

public class GetPlotsReadyForUavQueryHandler : IRequestHandler<GetPlotsReadyForUavQuery, Result<List<UavPlotReadinessResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotsReadyForUavQueryHandler> _logger;

    public GetPlotsReadyForUavQueryHandler(IUnitOfWork unitOfWork, ILogger<GetPlotsReadyForUavQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<UavPlotReadinessResponse>>> Handle(GetPlotsReadyForUavQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var todayUtc = DateTime.UtcNow.Date;
            
            // 1. Tải Group và các Plots trực tiếp của nó
            var group = await _unitOfWork.Repository<Group>()
                .FindAsync(
                    match: g => g.Id == request.GroupId, // ĐÃ SỬA LỖI: filter -> match
                    includeProperties: q => q.Include(g => g.GroupPlots).ThenInclude(gp => gp.Plot).ThenInclude(p => p.PlotCultivations)
                );

            if (group == null)
            {
                return Result<List<UavPlotReadinessResponse>>.Failure("Group not found.", "GroupNotFound");
            }
            
            var allPlotsInGroup = group.GroupPlots.Select(gp => gp.Plot).ToList();
            var allPlotIdsInGroup = allPlotsInGroup.Select(p => p.Id).ToList();

            // Lấy PlotCultivation IDs đang hoạt động trong Group này
            var plotCultivationIdSet = allPlotsInGroup
                .SelectMany(p => p.PlotCultivations)
                .Select(pc => pc.Id)
                .ToHashSet();


            // 2. Lọc ra các Plot đang có Active UAV Order Assignment (Tránh tạo đơn hàng trùng lặp)
            var busyAssignments = await _unitOfWork.Repository<UavOrderPlotAssignment>()
                .ListAsync(
                    filter: a => 
                        // Lọc theo GroupId thông qua PlotId
                        allPlotIdsInGroup.Contains(a.PlotId) && 
                        (a.Status == RiceProduction.Domain.Enums.TaskStatus.Draft || a.Status == RiceProduction.Domain.Enums.TaskStatus.PendingApproval || a.Status == RiceProduction.Domain.Enums.TaskStatus.InProgress)
                );
            var busyPlotIdSet = busyAssignments.Select(a => a.PlotId).Distinct().ToHashSet();


            // 3. Tải tất cả CultivationTasks tiềm năng và các mối quan hệ liên quan
            Expression<Func<CultivationTask, bool>> taskFilter = ct =>
                // Phải thuộc PlotCultivation đã xác định trong Group này
                plotCultivationIdSet.Contains(ct.PlotCultivationId) && 
                // Phải là Task Type phù hợp
                (ct.TaskType == request.RequiredTaskType);

            var potentialTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: taskFilter,
                includeProperties: q => q
                    .Include(ct => ct.PlotCultivation).ThenInclude(pc => pc.Plot)
                    .Include(ct => ct.ProductionPlanTask)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
            );

            if (!potentialTasks.Any())
            {
                return Result<List<UavPlotReadinessResponse>>.Success(new List<UavPlotReadinessResponse>(), $"No pending UAV-suitable tasks found in Group {request.GroupId}.");
            }
            
            // 4. Phân tích Điều kiện Sẵn sàng
            var readyPlots = new List<UavPlotReadinessResponse>();

            foreach (var task in potentialTasks)
            {
                var plot = task.PlotCultivation.Plot;
                var plotArea = task.PlotCultivation.Area.GetValueOrDefault(0M);
                
                var isScheduledSoon = task.ProductionPlanTask.ScheduledDate.Date <= todayUtc.AddDays(7) &&
                                      task.Status != RiceProduction.Domain.Enums.TaskStatus.Completed && 
                                      task.Status != RiceProduction.Domain.Enums.TaskStatus.Cancelled;

                var hasNoActiveUavOrder = !busyPlotIdSet.Contains(plot.Id);
                var dependenciesMet = true; // Giả sử các phụ thuộc đã được đáp ứng

                if (isScheduledSoon && hasNoActiveUavOrder && dependenciesMet)
                {
                    var estimatedMaterialCost = task.ProductionPlanTask.ProductionPlanTaskMaterials
                        .Sum(pptm => pptm.EstimatedAmount.GetValueOrDefault(0M));
                    
                    readyPlots.Add(new UavPlotReadinessResponse
                    {
                        PlotId = plot.Id,
                        PlotName = $"Thửa {plot.SoThua ?? 0}, Tờ {plot.SoTo ?? 0}",
                        PlotCultivationId = task.PlotCultivationId,
                        PlotArea = plotArea,
                        ReadyDate = task.ProductionPlanTask.ScheduledDate.Date,
                        TaskType = task.TaskType.GetValueOrDefault(TaskType.PestControl),
                        CultivationTaskName = task.CultivationTaskName ?? task.ProductionPlanTask.TaskName,
                        EstimatedMaterialCost = estimatedMaterialCost
                    });
                }
            }

            // Lọc các PlotId trùng lặp
            var uniqueReadyPlots = readyPlots.GroupBy(p => p.PlotId)
                                            .Select(g => g.First())
                                            .ToList();

            _logger.LogInformation("Found {Count} plots ready for UAV service in Group {GroupId}.", uniqueReadyPlots.Count, request.GroupId);

            return Result<List<UavPlotReadinessResponse>>.Success(uniqueReadyPlots, "Successfully retrieved plots ready for UAV service.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ready plots for UAV in Group {GroupId}", request.GroupId);
            return Result<List<UavPlotReadinessResponse>>.Failure("Failed to retrieve ready plots.", "GetReadyPlotsFailed");
        }
    }
}