using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

public class GetUavOrderDetailQueryHandler : IRequestHandler<GetUavOrderDetailQuery, Result<UavOrderDetailResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetUavOrderDetailQueryHandler> _logger;

    public GetUavOrderDetailQueryHandler(IApplicationDbContext context, ILogger<GetUavOrderDetailQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<UavOrderDetailResponse>> Handle(GetUavOrderDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await _context.UavServiceOrders
                .AsNoTracking()
                .Include(o => o.UavVendor)
                .Include(o => o.Creator)
                .Include(o => o.Group)
                    .ThenInclude(g => g.Cluster)
                .Include(o => o.PlotAssignments)
                    .ThenInclude(pa => pa.Plot)
                .Include(o => o.PlotAssignments)
                    .ThenInclude(pa => pa.CultivationTask)
                        .ThenInclude(ct => ct.CultivationTaskMaterials)
                            .ThenInclude(ctm => ctm.Material)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

            if (order == null)
            {
                return Result<UavOrderDetailResponse>.Failure("Service Order not found or unauthorized.", "OrderNotFound");
            }

            // --- Ánh xạ Plot Assignments với Materials từ CultivationTask ---
            var plotAssignmentsResponse = order.PlotAssignments
                .Select(pa =>
                {
                    // Lấy materials từ CultivationTask của plot assignment này
                    var taskMaterials = pa.CultivationTask?.CultivationTaskMaterials?
                        .Select(ctm => new PlannedMaterialDto
                        {
                            MaterialId = ctm.MaterialId,
                            MaterialName = ctm.Material.Name,
                            MaterialUnit = ctm.Material.Unit,
                            QuantityPerHa = ctm.ActualQuantity / (pa.Plot.Area > 0 ? pa.Plot.Area : 1),
                            TotalQuantityRequired = ctm.ActualQuantity,
                            TotalEstimatedCost = ctm.ActualCost
                        })
                        .ToList() ?? new List<PlannedMaterialDto>();

                    return new UavOrderPlotAssignmentResponse
                    {
                        PlotId = pa.PlotId,
                        PlotName = $"Thửa {pa.Plot.SoThua ?? 0} - Tờ {pa.Plot.SoTo ?? 0}",
                        ServicedArea = pa.ServicedArea,
                        Status = pa.Status,
                        ActualCost = pa.ActualCost,
                        CompletionDate = pa.CompletionDate,
                        ReportNotes = pa.ReportNotes,
                        
                        // Thông tin về Cultivation Task
                        CultivationTaskId = pa.CultivationTaskId,
                        CultivationTaskName = pa.CultivationTask?.CultivationTaskName 
                            ?? pa.CultivationTask?.ProductionPlanTask?.TaskName,
                        TaskType = pa.CultivationTask?.TaskType?.ToString(),
                        
                        // Gán dữ liệu GIS của Plot Boundary
                        PlotBoundaryGeoJson = pa.Plot.Boundary != null && !pa.Plot.Boundary.IsEmpty 
                            ? pa.Plot.Boundary.AsText() 
                            : null,
                        
                        // Deserialize ProofUrlsJson (an toàn)
                        ProofUrls = pa.ProofUrlsJson != null 
                            ? JsonSerializer.Deserialize<List<string>>(pa.ProofUrlsJson) ?? new List<string>() 
                            : new List<string>(),
                        
                        // Materials của cultivation task này
                        Materials = taskMaterials
                    };
                })
                .OrderBy(pa => pa.PlotName)
                .ToList();

            var response = new UavOrderDetailResponse
            {
                OrderId = order.Id,
                OrderName = order.OrderName,
                Status = order.Status,
                Priority = order.Priority,
                ScheduledDate = order.ScheduledDate,
                ScheduledTime = order.ScheduledTime,
                GroupId = order.GroupId,
                GroupName = order.Group.Cluster.ClusterName,
                TotalArea = order.TotalArea,
                TotalPlots = order.TotalPlots,
                EstimatedCost = order.EstimatedCost,
                
                // Chi phí và Tiến độ tổng hợp
                ActualCost = order.ActualCost,
                CompletionPercentage = order.CompletionPercentage,
                CompletedAt = order.CompletedAt,
                StartedAt = order.StartedAt,
                
                VendorName = order.UavVendor?.VendorName,
                CreatorName = order.Creator?.FullName,
                OptimizedRouteJson = order.OptimizedRoute != null 
                    ? order.OptimizedRoute.AsText()
                    : order.RouteData, // Route Data (LineString)
                
                PlotAssignments = plotAssignmentsResponse
            };

            return Result<UavOrderDetailResponse>.Success(response, "Successfully retrieved service order details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving UAV service order details for ID {OrderId}", request.OrderId);
            return Result<UavOrderDetailResponse>.Failure("Failed to retrieve order details.", "GetOrderDetailFailed");
        }
    }
}