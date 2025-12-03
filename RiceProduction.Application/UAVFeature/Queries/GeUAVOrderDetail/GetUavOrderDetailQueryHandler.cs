using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Linq;
using System;
using System.Text.Json;
using System.Text.Json.Serialization; 
using System.Collections.Generic;
using NetTopologySuite.Features; 
namespace RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

public class GetUavOrderDetailQueryHandler : IRequestHandler<GetUavOrderDetailQuery, Result<UavOrderDetailResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetUavOrderDetailQueryHandler> _logger;

    public GetUavOrderDetailQueryHandler(IUnitOfWork unitOfWork, ILogger<GetUavOrderDetailQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    private readonly JsonSerializerOptions _gisSerializerOptions = new JsonSerializerOptions
    {
        // Cho phép các giá trị số không hợp lệ (NaN, Infinity) được serialize (khắc phục lỗi)
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        // Cần thêm các tùy chọn NetTopologySuite nếu bạn sử dụng
        // ví dụ: Converters = { new GeoJsonConverterFactory() }
    };

    public async Task<Result<UavOrderDetailResponse>> Handle(GetUavOrderDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Tải Order với tất cả các mối quan hệ sâu, bao gồm các Plot Assignments
            var order = await _unitOfWork.Repository<UavServiceOrder>().FindAsync(
                match: o => o.Id == request.OrderId && o.UavVendorId == request.VendorId,
                includeProperties: q => q
                    .Include(o => o.UavVendor)
                    .Include(o => o.Creator)
                    .Include(o => o.Group).ThenInclude(g => g.Cluster)
                    .Include(o => o.PlotAssignments).ThenInclude(pa => pa.Plot) // <-- Load Plot
                    // Lấy Materials từ Production Plan (tương tự logic cũ)
                    .Include(o => o.Group).ThenInclude(g => g.ProductionPlans)
                        .ThenInclude(pp => pp.CurrentProductionStages)
                        .ThenInclude(ps => ps.ProductionPlanTasks)
                        .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                        .ThenInclude(pptm => pptm.Material)
            );

            if (order == null)
            {
                return Result<UavOrderDetailResponse>.Failure("Service Order not found or unauthorized.", "OrderNotFound");
            }
            
            // --- Logic tính toán và ánh xạ Vật tư (Materials) ---
            var materials = order.Group?.ProductionPlans
                .SelectMany(pp => pp.CurrentProductionStages)
                .SelectMany(ps => ps.ProductionPlanTasks)
                .SelectMany(ppt => ppt.ProductionPlanTaskMaterials)
                .GroupBy(pptm => pptm.MaterialId)
                .Select(g => 
                {
                    var firstMat = g.First();
                    var totalQtyPerHa = g.Sum(i => i.QuantityPerHa);
                    var totalEstimatedCost = g.Sum(i => i.EstimatedAmount.GetValueOrDefault(0M));
                    
                    return new PlannedMaterialDto
                    {
                        MaterialId = firstMat.MaterialId,
                        MaterialName = firstMat.Material.Name,
                        MaterialUnit = firstMat.Material.Unit,
                        QuantityPerHa = totalQtyPerHa / order.TotalPlots, 
                        TotalQuantityRequired = totalQtyPerHa * order.TotalArea,
                        TotalEstimatedCost = totalEstimatedCost
                    };
                })
                .ToList() ?? new List<PlannedMaterialDto>();

            // --- Ánh xạ Plot Assignments (MỚI) ---
            var plotAssignmentsResponse = order.PlotAssignments
                .Select(pa => new UavOrderPlotAssignmentResponse
                {
                    PlotId = pa.PlotId,
                    PlotName = $"Thửa {pa.Plot.SoThua ?? 0} - Tờ {pa.Plot.SoTo ?? 0}",
                    ServicedArea = pa.ServicedArea,
                    Status = pa.Status,
                    ActualCost = pa.ActualCost,
                    CompletionDate = pa.CompletionDate,
                    ReportNotes = pa.ReportNotes,
                    
                    // Gán dữ liệu GIS của Plot Boundary (Sử dụng JsonSerializer cho Polygon)
                    // Lưu ý: Cần đảm bảo thư viện NetTopologySuite đã được cấu hình để serialize sang GeoJSON
                     PlotBoundaryGeoJson = pa.Plot.Boundary != null && !pa.Plot.Boundary.IsEmpty 
                        ? pa.Plot.Boundary.AsText() 
                        : null,
                    
                    // Deserialize ProofUrlsJson (an toàn)
                    ProofUrls = pa.ProofUrlsJson != null ? JsonSerializer.Deserialize<List<string>>(pa.ProofUrlsJson) ?? new List<string>() : new List<string>()
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
                
                Materials = materials,
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