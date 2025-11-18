using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

namespace RiceProduction.Application.GroupFeature.Commands.FormGroups;

public class FormGroupsCommand : IRequest<Result<FormGroupsResponse>>
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public double? ProximityThreshold { get; set; }
    public int? PlantingDateTolerance { get; set; }
    public decimal? MinGroupArea { get; set; }
    public decimal? MaxGroupArea { get; set; }
    public int? MinPlotsPerGroup { get; set; }
    public int? MaxPlotsPerGroup { get; set; }
    public bool AutoAssignSupervisors { get; set; } = true;
    public bool CreateGroupsImmediately { get; set; } = true;
}

