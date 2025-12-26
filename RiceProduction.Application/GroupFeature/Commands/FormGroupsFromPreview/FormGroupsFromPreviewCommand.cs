using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;

namespace RiceProduction.Application.GroupFeature.Commands.FormGroupsFromPreview;

public class FormGroupsFromPreviewCommand : IRequest<Result<FormGroupsResponse>>
{
    public Guid ClusterId { get; set; }
    public Guid SeasonId { get; set; }
    public int Year { get; set; }
    public bool CreateGroupsImmediately { get; set; } = true;
    
    // The preview groups from frontend (potentially edited)
    public List<PreviewGroupInput> Groups { get; set; } = new();
}

// DTO for receiving edited preview groups from frontend
public class PreviewGroupInput
{
    // Group name that can be edited by frontend
    public string? GroupName { get; set; }
    
    public Guid RiceVarietyId { get; set; }
    public DateTime PlantingWindowStart { get; set; }
    public DateTime PlantingWindowEnd { get; set; }
    public DateTime MedianPlantingDate { get; set; }
    public List<Guid> PlotIds { get; set; } = new();
    
    // Allow frontend to specify or change supervisor
    public Guid? SupervisorId { get; set; }
}

