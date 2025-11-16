namespace RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

public class FormGroupsResponse
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public int GroupsCreated { get; set; }
    public int PlotsGrouped { get; set; }
    public int UngroupedPlots { get; set; }
    public List<CreatedGroupDto> Groups { get; set; } = new();
    public List<Guid> UngroupedPlotIds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class CreatedGroupDto
{
    public Guid GroupId { get; set; }
    public Guid RiceVarietyId { get; set; }
    public string RiceVarietyName { get; set; } = string.Empty;
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime PlantingDate { get; set; }
    public DateTime PlantingWindowStart { get; set; }
    public DateTime PlantingWindowEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PlotCount { get; set; }
    public decimal TotalArea { get; set; }
    public string? GroupBoundaryWkt { get; set; }
    public List<Guid> PlotIds { get; set; } = new();
}

