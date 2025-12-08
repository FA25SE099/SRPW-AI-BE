using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Linq;
using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace RiceProduction.Application.UAVFeature.Commands.CreateUavOrder;

public class CreateUavOrderCommandHandler : IRequestHandler<CreateUavOrderCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateUavOrderCommandHandler> _logger;
    private readonly GeometryFactory _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

    public CreateUavOrderCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateUavOrderCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateUavOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Xác thực người dùng và tải Group/Vendor
            if (request.ClusterManagerId == null)
            {
                return Result<Guid>.Failure("Cluster Manager ID is required.", "Unauthorized");
            }
            
            // Tải Group cùng với Plots và PlotCultivations (để lấy Area)
            var group = await _unitOfWork.Repository<Group>().FindAsync(
                g => g.Id == request.GroupId,
                includeProperties: q => q.Include(g => g.GroupPlots).ThenInclude(gp => gp.Plot).ThenInclude(p => p.PlotCultivations)
            );
            var vendor = await _unitOfWork.UavVendorRepository.GetUavVendorByIdAsync(request.UavVendorId);

            if (group == null) return Result<Guid>.Failure("Group not found.", "GroupNotFound");
            if (vendor == null) return Result<Guid>.Failure("UAV Vendor not found.", "VendorNotFound");
            if (group.TotalArea == null || group.TotalArea.Value <= 0) 
            {
                return Result<Guid>.Failure("Group area is invalid.", "GroupAreaInvalid");
            }
            
            // --- 2. Tải và Xác định CultivationTask đang hoạt động cho các Plots đã chọn ---
            var selectedPlots = group.GroupPlots.Select(gp => gp.Plot).Where(p => request.SelectedPlotIds.Contains(p.Id)).ToList();
            // Lấy tất cả PlotCultivation ID của các Plot đã chọn
            var selectedPlotCultivationIds = selectedPlots
                .SelectMany(p => p.PlotCultivations)
                .Where(pc => pc.Status == CultivationStatus.Planned || pc.Status == CultivationStatus.InProgress || pc.Status == CultivationStatus.Completed)
                .Select(pc => pc.Id)
                .ToList();
            
            // Tìm CultivationTask phù hợp (Spraying/Fertilization)
            var cultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => 
                    selectedPlotCultivationIds.Contains(ct.PlotCultivationId) 
                    &&
                    (ct.TaskType == TaskType.Harvesting || ct.TaskType == TaskType.LandPreparation
                    || ct.TaskType == TaskType.PestControl || ct.TaskType == TaskType.Fertilization ) 
                    &&
                    (ct.Status == RiceProduction.Domain.Enums.TaskStatus.Draft || ct.Status == RiceProduction.Domain.Enums.TaskStatus.Approved
                    || ct.Status == RiceProduction.Domain.Enums.TaskStatus.InProgress 
                    || ct.Status == RiceProduction.Domain.Enums.TaskStatus.Completed) // Task đang chờ thực hiện
            );

            if (cultivationTasks.Count == 0)
            {
                return Result<Guid>.Failure("No active Cultivation Tasks (Spraying/Fertilization) found for the selected plots.", "NoActiveTasks");
            }

            // 3. Tính toán các thông số
            var totalArea = group.TotalArea.Value;
            var estimatedCost = totalArea * vendor.ServiceRatePerHa;
            var totalPlots = request.SelectedPlotIds.Count; // Tổng Plots được chọn
            var orderName = request.OrderNameOverride ?? $"{group.Cluster.ClusterName} - Dịch vụ UAV {DateTime.Now:yyyyMMdd}";

            var plotCoordinates = selectedPlots
                .Where(p => p.Coordinate != null)
                .OrderBy(p => p.Coordinate!.X) // Sắp xếp thô để tạo đường
                .Select(p => p.Coordinate!.Coordinate)
                .ToArray();
                LineString? optimizedRoute = null;
            if (plotCoordinates.Length >= 2)
            {
                optimizedRoute = _geometryFactory.CreateLineString(plotCoordinates);
            } else if (plotCoordinates.Length == 1) {
                // Nếu chỉ có 1 điểm, có thể tạo Point hoặc để null, nhưng không thể tạo LineString.
                // Chúng ta sẽ để null và dựa vào RouteData để hiển thị Plot.
                _logger.LogWarning("Only one plot selected. Cannot generate LineString route.");
            }

            
            // Lưu ranh giới Plot để hiển thị bản đồ trong app Vendor
            var plotBoundaries = selectedPlots
                .Select(p => new { Id = p.Id, Boundary = p.Boundary?.AsText(), Coordinate = p.Coordinate?.AsText() })
                .ToList();
            
            var routeDataJson = JsonSerializer.Serialize(plotBoundaries);

            // 4. Tạo UavServiceOrder Entity
            var uavOrder = new UavServiceOrder
            {
                GroupId = request.GroupId,
                UavVendorId = request.UavVendorId,
                OrderName = orderName,
                ScheduledDate = DateTime.SpecifyKind(request.ScheduledDate, DateTimeKind.Utc),
                Status = RiceProduction.Domain.Enums.TaskStatus.Approved,
                Priority = request.Priority,
                TotalArea = totalArea,
                TotalPlots = totalPlots,
                EstimatedCost = estimatedCost,
                CreatedBy = request.ClusterManagerId.Value,
            };

            // 5. Tạo UavOrderPlotAssignments cho từng CultivationTask/Plot
            var assignments = new List<UavOrderPlotAssignment>();
            
            // Gom nhóm Tasks theo PlotId (để đảm bảo mỗi Plot chỉ có một Assignment)
            var tasksByPlot = cultivationTasks.GroupBy(ct => ct.PlotCultivation.PlotId);

            foreach (var groupOfTasks in tasksByPlot)
            {
                var plotId = groupOfTasks.Key;
                var singleTask = groupOfTasks.First(); // Lấy một Task để đại diện cho Plot
                var plot = group.GroupPlots.Select(gp => gp.Plot).First(p => p.Id == plotId);

                assignments.Add(new UavOrderPlotAssignment
                {
                    UavServiceOrderId = uavOrder.Id,
                    PlotId = plotId,
                    CultivationTaskId = singleTask.Id, // Gán FK trực tiếp đến CultivationTask
                    ServicedArea = plot.Area, 
                    Status = RiceProduction.Domain.Enums.TaskStatus.PendingApproval
                });
            }

            uavOrder.PlotAssignments = assignments;
            
            // 6. Lưu DB
            await _unitOfWork.Repository<UavServiceOrder>().AddAsync(uavOrder);
            await _unitOfWork.Repository<UavServiceOrder>().SaveChangesAsync();

            _logger.LogInformation("UAV Order {OrderId} created successfully for Group {GroupId}. Cost: {Cost}", uavOrder.Id, request.GroupId, estimatedCost);
            
            return Result<Guid>.Success(uavOrder.Id, $"UAV Order '{orderName}' created and assigned to vendor.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UAV order for Group {GroupId}", request.GroupId);
            return Result<Guid>.Failure("Failed to create UAV order.", "CreateOrderFailed");
        }
    }
}