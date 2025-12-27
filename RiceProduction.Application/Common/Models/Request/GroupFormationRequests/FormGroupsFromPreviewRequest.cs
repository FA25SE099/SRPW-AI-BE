using System.ComponentModel.DataAnnotations;

namespace RiceProduction.Application.Common.Models.Request.GroupFormationRequests;

public class FormGroupsFromPreviewRequest
{
    [Required]
    public Guid ClusterId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    public int Year { get; set; }

    public bool CreateGroupsImmediately { get; set; } = true;

    [Required]
    public List<PreviewGroupInputRequest> Groups { get; set; } = new();
}

public class PreviewGroupInputRequest
{
    public string? GroupName { get; set; }
    
    [Required]
    public Guid RiceVarietyId { get; set; }
    
    [Required]
    public DateTime PlantingWindowStart { get; set; }
    
    [Required]
    public DateTime PlantingWindowEnd { get; set; }
    
    [Required]
    public DateTime MedianPlantingDate { get; set; }
    
    [Required]
    public List<Guid> PlotIds { get; set; } = new();
    
    public Guid? SupervisorId { get; set; }
}

