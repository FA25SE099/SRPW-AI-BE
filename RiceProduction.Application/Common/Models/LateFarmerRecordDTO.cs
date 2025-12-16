namespace RiceProduction.Application.Common.Models;

public class LateFarmerRecordDTO
{
    public Guid Id { get; set; }
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public Guid TaskId { get; set; }
    public string? TaskName { get; set; }
    public Guid PlotId { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public Guid PlotCultivationId { get; set; }
    public Guid SeasonId { get; set; }
    public string? SeasonName { get; set; }
    public Guid GroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid ClusterId { get; set; }
    public string? ClusterName { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Notes { get; set; }
}

public class FarmerLateCountDTO
{
    public Guid FarmerId { get; set; }
    public int LateCount { get; set; }
}

public class PlotLateCountDTO
{
    public Guid PlotId { get; set; }
    public int LateCount { get; set; }
}

public class FarmerLateDetailDTO
{
    public Guid FarmerId { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? FarmCode { get; set; }
    public int TotalLateCount { get; set; }
    public IEnumerable<LateFarmerRecordDTO> LateRecords { get; set; } = new List<LateFarmerRecordDTO>();
}

public class FarmerWithLateCountDTO
{
    public Guid FarmerId { get; set; }
    public string? FullName { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? FarmCode { get; set; }
    public int PlotCount { get; set; }
    public int LateCount { get; set; }
}

public class PlotWithLateCountDTO
{
    public Guid PlotId { get; set; }
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public Guid? GroupId { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal Area { get; set; }
    public string? SoilType { get; set; }
    public Domain.Enums.PlotStatus Status { get; set; }
    public int LateCount { get; set; }
}
