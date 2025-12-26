using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;

/// <summary>
/// DTO cho chi tiết Assignment của một Plot trong Order (sử dụng trong Detail View).
/// </summary>
public class UavOrderPlotAssignmentResponse
{
    public Guid PlotId { get; set; }
    public string PlotName { get; set; } = string.Empty; // Thửa {SOTHO} - Tờ {SOTO}
    public decimal ServicedArea { get; set; }
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public decimal? ActualCost { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? ReportNotes { get; set; }
    
    /// <summary>
    /// Dữ liệu hình học của Plot để hiển thị trên bản đồ (GeoJSON/WKT string).
    /// </summary>
    public string? PlotBoundaryGeoJson { get; set; } 

    // Deserialize từ ProofUrlsJson trong Entity
    public List<string> ProofUrls { get; set; } = new();
    
    /// <summary>
    /// Thông tin công việc canh tác được giao cho Plot này
    /// </summary>
    public Guid? CultivationTaskId { get; set; }
    public string? CultivationTaskName { get; set; }
    public string? TaskType { get; set; }
    
    /// <summary>
    /// Danh sách vật tư cần thiết cho công việc canh tác của Plot này
    /// </summary>
    public List<PlannedMaterialDto> Materials { get; set; } = new();
}

/// <summary>
/// DTO cho Vật tư Kế hoạch cần dùng (trong Detail và List View).
/// </summary>
public class PlannedMaterialDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnit { get; set; } = string.Empty;
    public decimal QuantityPerHa { get; set; }
    public decimal TotalQuantityRequired { get; set; }
    public decimal TotalEstimatedCost { get; set; }
}

/// <summary>
/// DTO cho một đơn hàng UAV trong danh sách (List View).
/// </summary>
public class UavServiceOrderResponse
{
    public Guid OrderId { get; set; }
    public string OrderName { get; set; } = string.Empty;
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    
    // Lịch trình
    public DateTime ScheduledDate { get; set; }
    public TimeSpan? ScheduledTime { get; set; }
    
    // Thông tin khu vực
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty; // Tên Group (hoặc Cluster)
    public decimal TotalArea { get; set; }
    public int TotalPlots { get; set; }
    
    // Chi phí và Tiến độ
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public int CompletionPercentage { get; set; }
    public string? CreatorName { get; set; }
}

/// <summary>
/// DTO Chi tiết đơn hàng UAV (cho GET /orders/{orderId}).
/// </summary>
public class UavOrderDetailResponse : UavServiceOrderResponse
{
    public string? VendorName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    /// <summary>
    /// Dữ liệu tuyến đường tối ưu (GeoJSON/WKT string).
    /// </summary>
    public string? OptimizedRouteJson { get; set; } 

    public List<UavOrderPlotAssignmentResponse> PlotAssignments { get; set; } = new(); // Danh sách tiến độ Plot
}