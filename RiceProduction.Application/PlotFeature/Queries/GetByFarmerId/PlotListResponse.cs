using NetTopologySuite.Geometries;
using RiceProduction.Domain.Enums;
namespace RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;

public class PlotListResponse
{
    public Guid PlotId { get; set; }
    public decimal Area { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public PlotStatus Status { get; set; }
    public Guid? GroupId { get; set; }
    public string? Boundary { get; set; }
    public string? Coordinate { get; set; }
    /// Tên của Cluster mà Group này thuộc về.
    public string? GroupName { get; set; }
    /// Số lượng mùa vụ đang canh tác (Planned hoặc InProgress).
    public int ActiveCultivations { get; set; }

    /// Số lượng cảnh báo đang mở (New hoặc Acknowledged).
    public int ActiveAlerts { get; set; }
}