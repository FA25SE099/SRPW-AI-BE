using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Request.SupervisorRequests;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;
using RiceProduction.Application.SupervisorFeature.Queries;
using RiceProduction.Application.SupervisorFeature.Queries.GetPolygonAssignmentTasks;
using RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;
using RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupThisSeason;
using RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupHistory;
using RiceProduction.Application.SupervisorFeature.Queries.ViewGroupBySeason;
using RiceProduction.Application.SupervisorFeature.Queries.GetPlanDetails;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorAvailableSeasons;
using Microsoft.AspNetCore.Authorization;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController : Controller
{
    private readonly IMediator _mediator;

    public SupervisorController(IMediator mediator)
    {
        _mediator = mediator;
    }


    [HttpPost("get-paging")]
    public async Task<ActionResult<PagedResult<List<SupervisorResponse>>>> GetAllSupervisorPaging([FromForm] SupervisorListRequest request)
    {
        //var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //if (string.IsNullOrEmpty(userId))
        //{
        //    return Unauthorized(PagedResult<List<SupervisorResponse>>.Failure("User not authenticated"));
        //}

        //if (!Guid.TryParse(userId, out var userIdReal) || userIdReal == Guid.Empty)
        //{
        //    return Unauthorized(PagedResult<List<SupervisorResponse>>.Failure("Invalid user ID"));
        //}
        var query = new GetAllSupervisorQuery
        {
            ClusterManagerUserId = new Guid("019a0806-24ef-7df0-ac28-74495da52a12"),
            SearchNameOrEmail = request.SearchNameOrEmail,
            SearchPhoneNumber = request.SearchPhoneNumber,
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

    /// <summary>
    /// Get polygon assignment tasks for the current supervisor
    /// </summary>
    [HttpGet("polygon-tasks")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<List<PlotPolygonTaskDto>>>> GetMyPolygonTasks([FromQuery] string? status = null)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<List<PlotPolygonTaskDto>>.Failure("User not authenticated"));
        }

        var query = new GetPolygonAssignmentTasksQuery
        {
            SupervisorId = supervisorId,
            Status = status
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Complete a polygon assignment task by providing the plot boundary
    /// </summary>
    [HttpPost("polygon/{taskId}/complete")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<bool>>> CompletePolygonTask(
        Guid taskId,
        [FromBody] CompletePolygonRequest request)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<bool>.Failure("User not authenticated"));
        }

        var command = new CompletePolygonAssignmentCommand
        {
            TaskId = taskId,
            SupervisorId = supervisorId,
            PolygonGeoJson = request.PolygonGeoJson,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get group overview by season and year (lightweight - fast loading)
    /// If seasonId and year are not provided, returns current season
    /// </summary>
    [HttpGet("group-by-season")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<GroupBySeasonResponse>>> GetGroupBySeason(
        [FromQuery] Guid? seasonId = null,
        [FromQuery] int? year = null)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<GroupBySeasonResponse>.Failure("User not authenticated"));
        }

        var query = new ViewGroupBySeasonQuery
        {
            SupervisorId = supervisorId,
            SeasonId = seasonId,
            Year = year
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get detailed production plan progress (heavy - includes all tasks and materials)
    /// </summary>
    [HttpGet("plan/{planId}/details")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<PlanDetailsResponse>>> GetPlanDetails(Guid planId)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<PlanDetailsResponse>.Failure("User not authenticated"));
        }

        var query = new GetPlanDetailsQuery
        {
            SupervisorId = supervisorId,
            ProductionPlanId = planId
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get list of available seasons for dropdown selector
    /// Returns all season+year combinations where supervisor has a group
    /// </summary>
    [HttpGet("available-seasons")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<List<AvailableSeasonYearDto>>>> GetAvailableSeasons()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<List<AvailableSeasonYearDto>>.Failure("User not authenticated"));
        }

        var query = new GetSupervisorAvailableSeasonsQuery
        {
            SupervisorId = supervisorId
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// [DEPRECATED] Use /group-by-season instead
    /// Get current group information
    /// </summary>
    [HttpGet("my-group")]
    [Authorize(Roles = "Supervisor")]
    [Obsolete("Use /group-by-season endpoint instead")]
    public async Task<ActionResult<Result<MyGroupResponse>>> GetMyGroup([FromQuery] Guid? seasonId = null)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<MyGroupResponse>.Failure("User not authenticated"));
        }

        var query = new GetMyGroupThisSeasonQuery
        {
            SupervisorId = supervisorId,
            SeasonId = seasonId
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// [DEPRECATED] Use /group-by-season with seasonId and year parameters instead
    /// Get historical groups
    /// </summary>
    [HttpGet("group-history")]
    [Authorize(Roles = "Supervisor")]
    [Obsolete("Use /group-by-season endpoint instead")]
    public async Task<ActionResult<Result<List<GroupHistorySummary>>>> GetGroupHistory(
        [FromQuery] int? year = null,
        [FromQuery] bool includeCurrentSeason = false)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var supervisorId))
        {
            return Unauthorized(Result<List<GroupHistorySummary>>.Failure("User not authenticated"));
        }

        var query = new GetMyGroupHistoryQuery
        {
            SupervisorId = supervisorId,
            Year = year,
            IncludeCurrentSeason = includeCurrentSeason
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public class CompletePolygonRequest
{
    public string PolygonGeoJson { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
