using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request.GroupFormationRequests;

public class FormGroupsRequest
{
    [Required]
    public Guid ClusterId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    public int Year { get; set; }

    public GroupingParametersRequest? Parameters { get; set; }

    public bool AutoAssignSupervisors { get; set; } = true;

    public bool CreateGroupsImmediately { get; set; } = true;
}

public class GroupingParametersRequest
{
    public double? ProximityThreshold { get; set; } // meters, default 2000
    public int? PlantingDateTolerance { get; set; } // days, default 2
    public decimal? MinGroupArea { get; set; } // hectares, default 15
    public decimal? MaxGroupArea { get; set; } // hectares, default 50
    public int? MinPlotsPerGroup { get; set; } // default 5
    public int? MaxPlotsPerGroup { get; set; } // default 15
}

