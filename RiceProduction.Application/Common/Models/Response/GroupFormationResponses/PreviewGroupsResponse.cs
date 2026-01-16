namespace RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

public class PreviewGroupsResponse
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public GroupingParametersDto Parameters { get; set; } = new();
    public PreviewSummary Summary { get; set; } = new();
    public List<SupervisorForAssignmentDto> AvailableSupervisors { get; set; } = new();
    public List<PreviewGroupDto> PreviewGroups { get; set; } = new();
    public List<UngroupedPlotDto> UngroupedPlots { get; set; } = new();
}

public class GroupingParametersDto
{
    public double ProximityThreshold { get; set; }
    public int PlantingDateTolerance { get; set; }
    public decimal MinGroupArea { get; set; }
    public decimal MaxGroupArea { get; set; }
    public int MinPlotsPerGroup { get; set; }
    public int MaxPlotsPerGroup { get; set; }
}

public class PreviewSummary
{
    public int TotalEligiblePlots { get; set; }
    public int PlotsGrouped { get; set; }
    public int UngroupedPlots { get; set; }
    public int GroupsToBeFormed { get; set; }
    public decimal EstimatedTotalArea { get; set; }
    public int SupervisorsNeeded { get; set; }
    public int SupervisorsAvailable { get; set; }
    public int GroupsWithoutSupervisor { get; set; }
    public bool HasSufficientSupervisors { get; set; }
}

public class PreviewGroupDto
{
    public int GroupNumber { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime PlantingWindowStart { get; set; }
    public DateTime PlantingWindowEnd { get; set; }
    public DateTime MedianPlantingDate { get; set; }
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
    public double CentroidLat { get; set; }
    public double CentroidLng { get; set; }
    public string? GroupBoundaryGeoJson { get; set; }
    public List<Guid> PlotIds { get; set; } = new();
    public List<PlotInGroupDto> Plots { get; set; } = new();
}

public class PlotInGroupDto
{
    public Guid PlotId { get; set; }
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    public decimal Area { get; set; }
    public DateTime PlantingDate { get; set; }
    public string? BoundaryGeoJson { get; set; }
    public string? SoilType { get; set; }
    public int? SoThua { get; set; }
    public int? SoTo { get; set; }
}

public class CoordinateDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class UngroupedPlotDto
{
    public Guid PlotId { get; set; }
    public Guid FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public string? FarmerPhone { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public DateTime PlantingDate { get; set; }
    public decimal Area { get; set; }
    public string? BoundaryGeoJson { get; set; }
    public string UngroupReason { get; set; } = string.Empty;
    public string ReasonDescription { get; set; } = string.Empty;
    public double? DistanceToNearestGroup { get; set; }
    public int? NearestGroupNumber { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public List<NearbyGroupInfo> NearbyGroups { get; set; } = new();
}

public class NearbyGroupInfo
{
    public int GroupNumber { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public double Distance { get; set; }
    public int PlantingDateDiffDays { get; set; }
    public bool IsCompatible { get; set; }
    public string? IncompatibilityReason { get; set; }
}

public class SupervisorForAssignmentDto
{
    public Guid SupervisorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public Guid? ClusterId { get; set; }
    public string? ClusterName { get; set; }
    public int CurrentFarmerCount { get; set; }
    public int MaxFarmerCapacity { get; set; }
    public int CurrentGroupCount { get; set; }
    public decimal CurrentTotalArea { get; set; }
    public decimal? MaxAreaCapacity { get; set; }
    public decimal? RemainingAreaCapacity { get; set; }
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
}

