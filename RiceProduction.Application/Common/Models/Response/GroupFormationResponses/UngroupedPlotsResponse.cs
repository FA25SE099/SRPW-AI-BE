namespace RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

public class UngroupedPlotsResponse
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public int TotalUngroupedPlots { get; set; }
    public decimal TotalArea { get; set; }
    public List<UngroupedPlotDetailDto> UngroupedPlots { get; set; } = new();
    public GroupingStatistics Statistics { get; set; } = new();
}

public class UngroupedPlotDetailDto
{
    public Guid PlotId { get; set; }
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public DateTime PlantingDate { get; set; }
    public decimal Area { get; set; }
    public CoordinateDto? Coordinate { get; set; }
    public string? BoundaryWkt { get; set; }
    public string UngroupReason { get; set; } = string.Empty;
    public string ReasonDetails { get; set; } = string.Empty;
    public List<NearestGroupInfo> NearestGroups { get; set; } = new();
    public List<RecommendedAction> RecommendedActions { get; set; } = new();
}

public class NearestGroupInfo
{
    public Guid GroupId { get; set; }
    public int GroupNumber { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public double Distance { get; set; }
    public int PlantingDateDiff { get; set; }
    public bool IsCompatible { get; set; }
    public string? IncompatibilityReason { get; set; }
}

public class RecommendedAction
{
    public string Action { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public int? GroupNumber { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class GroupingStatistics
{
    public Dictionary<string, int> ByReason { get; set; } = new();
    public Dictionary<string, int> ByVariety { get; set; } = new();
}

