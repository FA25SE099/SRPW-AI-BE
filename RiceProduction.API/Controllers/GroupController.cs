using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.GroupRequests;
using RiceProduction.Application.Common.Models.Request.GroupFormationRequests;
using RiceProduction.Application.Common.Models.Response.GroupResponses;
using RiceProduction.Application.Common.Models.Response.GroupFormationResponses;
using RiceProduction.Application.GroupFeature.Queries.GetAllGroup;
using RiceProduction.Application.GroupFeature.Queries.GetGroupDetail;
using RiceProduction.Application.GroupFeature.Queries.GetGroupsByClusterId;
using RiceProduction.Application.GroupFeature.Queries.PreviewGroups;
using RiceProduction.Application.GroupFeature.Commands.FormGroups;
using RiceProduction.Application.GroupFeature.Commands.FormGroupsFromPreview;
using RiceProduction.Application.GroupFeature.Commands.CreateGroupManually;
using RiceProduction.Application.GroupFeature.Queries.GetGroupByClusterManager;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GroupController : Controller
{
    private readonly IMediator _mediator;
    private readonly IUser _currentUser;

    public GroupController(IMediator mediator, IUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpPost()]
    public async Task<ActionResult<PagedResult<List<GroupResponse>>>> GetGroupsByClusterIdPaging([FromBody] GroupListRequest request)
    {
        
        var query = new GetGroupsByClusterManagerIdQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetGroupDetail(Guid id)
    {
        var result = await _mediator.Send(new GetGroupDetailQuery { GroupId = id });

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> GetAllGroups()    
    {
        var query = new GetAllGroupQuery();
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Preview automatic group formation without creating groups
    /// </summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(Result<PreviewGroupsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewGroups(
        [FromQuery] Guid clusterId,
        [FromQuery] Guid seasonId,
        [FromQuery] int year,
        [FromQuery] double? proximityThreshold = null,
        [FromQuery] int? plantingDateTolerance = null,
        [FromQuery] decimal? minGroupArea = null,
        [FromQuery] decimal? maxGroupArea = null,
        [FromQuery] int? minPlotsPerGroup = null,
        [FromQuery] int? maxPlotsPerGroup = null)
    {
        var query = new PreviewGroupsQuery
        {
            ClusterId = clusterId,
            SeasonId = seasonId,
            Year = year,
            ProximityThreshold = proximityThreshold,
            PlantingDateTolerance = plantingDateTolerance,
            MinGroupArea = minGroupArea,
            MaxGroupArea = maxGroupArea,
            MinPlotsPerGroup = minPlotsPerGroup,
            MaxPlotsPerGroup = maxPlotsPerGroup
        };

        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Automatically form groups based on spatial and temporal clustering
    /// </summary>
    [HttpPost("form")]
    [ProducesResponseType(typeof(Result<FormGroupsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FormGroups([FromBody] FormGroupsRequest request)
    {
        var command = new FormGroupsCommand
        {
            ClusterId = request.ClusterId,
            SeasonId = request.SeasonId,
            Year = request.Year,
            ProximityThreshold = request.Parameters?.ProximityThreshold,
            PlantingDateTolerance = request.Parameters?.PlantingDateTolerance,
            MinGroupArea = request.Parameters?.MinGroupArea,
            MaxGroupArea = request.Parameters?.MaxGroupArea,
            MinPlotsPerGroup = request.Parameters?.MinPlotsPerGroup,
            MaxPlotsPerGroup = request.Parameters?.MaxPlotsPerGroup,
            AutoAssignSupervisors = request.AutoAssignSupervisors,
            CreateGroupsImmediately = request.CreateGroupsImmediately
        };

        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Create groups from preview results (edited or unedited)
    /// </summary>
    [HttpPost("form-from-preview")]
    [ProducesResponseType(typeof(Result<FormGroupsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FormGroupsFromPreview([FromBody] FormGroupsFromPreviewRequest request)
    {
        var command = new FormGroupsFromPreviewCommand
        {
            ClusterId = request.ClusterId,
            SeasonId = request.SeasonId,
            Year = request.Year,
            CreateGroupsImmediately = request.CreateGroupsImmediately,
            Groups = request.Groups.Select(g => new PreviewGroupInput
            {
                GroupName = g.GroupName,
                RiceVarietyId = g.RiceVarietyId,
                PlantingWindowStart = g.PlantingWindowStart,
                PlantingWindowEnd = g.PlantingWindowEnd,
                MedianPlantingDate = g.MedianPlantingDate,
                PlotIds = g.PlotIds,
                SupervisorId = g.SupervisorId
            }).ToList()
        };

        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Manually create a group (for exceptions or ungrouped plots)
    /// </summary>
    [HttpPost("create-manual")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateGroupManually([FromBody] CreateGroupManuallyRequest request)
    {
        var command = new CreateGroupManuallyCommand
        {
            ClusterId = request.ClusterId,
            SupervisorId = request.SupervisorId,
            RiceVarietyId = request.RiceVarietyId,
            SeasonId = request.SeasonId,
            Year = request.Year,
            PlantingDate = request.PlantingDate,
            PlotIds = request.PlotIds,
            IsException = request.IsException,
            ExceptionReason = request.ExceptionReason
        };

        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpGet("my-groups")]
    public async Task<IActionResult> GetManagedGroups([FromQuery] GetGroupsByManagerQuery query)
    {
        var managerId = _currentUser.Id;
        if (managerId == null)
        {
            return Unauthorized(PagedResult<List<ClusterManagerGroupResponse>>.Failure("Cluster Manager authentication required.", "Unauthorized"));
        }
        
        query.ClusterManagerId = managerId.Value;

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

}
