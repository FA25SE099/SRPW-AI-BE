using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupThisSeason;

public class GetMyGroupThisSeasonQuery : IRequest<Result<MyGroupResponse>>
{
    public Guid SupervisorId { get; set; }
    public Guid? SeasonId { get; set; } // Optional - if null, uses current active season
}

public class MyGroupResponse
{
    // Group Basic Info
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlantingDate { get; set; }
    
    // Area Information
    public decimal? TotalArea { get; set; }
    public string? AreaGeoJson { get; set; }
    
    // Rice Variety
    public Guid? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    
    // Season
    public SeasonInfo Season { get; set; } = new();
    
    // Cluster
    public Guid ClusterId { get; set; }
    public string? ClusterName { get; set; }
    
    // Plots List with Full Details
    public List<PlotDetail> Plots { get; set; } = new();
    
    // Readiness Status for Production Plan
    public GroupReadinessInfo Readiness { get; set; } = new();
    
    // Production Plans Summary
    public ProductionPlansSummary ProductionPlans { get; set; } = new();
}

public class SeasonInfo
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public string SeasonType { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class PlotDetail
{
    public Guid PlotId { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
    public decimal Area { get; set; }
    public string? SoilType { get; set; }
    
    // Polygon Status
    public bool HasPolygon { get; set; }
    public string? PolygonGeoJson { get; set; }
    public string? CentroidGeoJson { get; set; }
    
    public string Status { get; set; } = string.Empty;
    
    // Farmer Information
    public Guid FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public string? FarmerPhone { get; set; }
    public string? FarmerAddress { get; set; }
    public string? FarmCode { get; set; }
}

public class GroupReadinessInfo
{
    public bool IsReady { get; set; }
    public int ReadinessScore { get; set; } // 0-100
    public string ReadinessLevel { get; set; } = string.Empty; // "Ready", "Almost Ready", "Not Ready"
    
    // Detailed Checks
    public bool HasRiceVariety { get; set; }
    public bool HasTotalArea { get; set; }
    public bool HasPlots { get; set; }
    public bool AllPlotsHavePolygons { get; set; }
    
    // Issues
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    // Polygon Statistics
    public int TotalPlots { get; set; }
    public int PlotsWithPolygon { get; set; }
    public int PlotsWithoutPolygon { get; set; }
}

public class ProductionPlansSummary
{
    public int TotalPlans { get; set; }
    public int ActivePlans { get; set; }
    public int DraftPlans { get; set; }
    public int ApprovedPlans { get; set; }
    public bool HasActiveProductionPlan { get; set; }
}

