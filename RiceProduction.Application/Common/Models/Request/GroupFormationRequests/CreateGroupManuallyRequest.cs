using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request.GroupFormationRequests;

public class CreateGroupManuallyRequest
{
    [Required]
    public Guid ClusterId { get; set; }

    public Guid? SupervisorId { get; set; }

    [Required]
    public Guid RiceVarietyId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    public int Year { get; set; }

    [Required]
    public DateTime PlantingDate { get; set; }

    [Required]
    public List<Guid> PlotIds { get; set; } = new();

    public bool IsException { get; set; } = false;

    public string? ExceptionReason { get; set; }
}

