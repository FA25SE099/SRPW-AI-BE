using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.SupervisorRequests;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Application.ReportFeature.Queries.GetReportsBySupervisor;
using RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;
using RiceProduction.Application.SupervisorFeature.Commands.CreateSupervisor;
using RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisor;
using RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForAdmin;
using RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForClusterManager;
using RiceProduction.Application.SupervisorFeature.Queries.GetFarmersBySupervisor;
using RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupHistory;
using RiceProduction.Application.SupervisorFeature.Queries.GetMyGroupThisSeason;
using RiceProduction.Application.SupervisorFeature.Queries.GetPlanDetails;
using RiceProduction.Application.SupervisorFeature.Queries.GetPolygonAssignmentTasks;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorAvailableSeasons;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorByClusterId;
using RiceProduction.Application.SupervisorFeature.Queries.GetSupervisorProfile;
using RiceProduction.Application.SupervisorFeature.Queries.ValidatePolygonArea;
using RiceProduction.Application.SupervisorFeature.Queries.ViewGroupBySeason;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<SupervisorController> _logger;
    private readonly IUser _currentUser;

    public SupervisorController(IMediator mediator, ILogger<SupervisorController> logger, IUser currentUser)
    {
        _mediator = mediator;
        _logger = logger;
        _currentUser = currentUser;
    }

    [HttpPost("get-supervisor-by-clustermanager-paging")]
    public async Task<ActionResult<PagedResult<List<SupervisorResponse>>>> GetAllSupervisorOfAClusterPaging([FromForm] SupervisorListRequest request)
    {
        var query = new GetAllSupervisorQuery
        {
            SearchNameOrEmail = request.SearchNameOrEmail,
            SearchPhoneNumber = request.SearchPhoneNumber,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };
        return this.ToActionResult(await _mediator.Send(query));
    }

    [HttpPost("get-all-supervisor-admin")]
    public async Task<ActionResult<PagedResult<List<SupervisorResponse>>>> GetSupervisorsPagingAndSearchForAdmin([FromBody] GetAllSupervisorForAdminQuery query)
    {
        return this.ToActionResult(await _mediator.Send(query));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSupervisorCommand([FromBody] CreateSupervisorCommand command)
    {
        return this.ToIActionResult(await _mediator.Send(command));
    }

    /// <summary>
    /// Get current supervisor's profile information
    /// </summary>
    [HttpGet("profile")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<SupervisorProfileResponse>>> GetMyProfile()
    {
        var supervisorId = _currentUser.Id;
        if (supervisorId == Guid.Empty)
        {
            return Unauthorized(Result<SupervisorProfileResponse>.Failure("User not authenticated"));
        }

        var query = new GetSupervisorProfileQuery
        {
            SupervisorId = supervisorId.Value
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
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
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(Result<List<PlotPolygonTaskDto>>.Failure("User not authenticated"));
        }

        var query = new GetPolygonAssignmentTasksQuery
        {
            SupervisorId = supervisorId,
            Status = status
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Validate polygon area against plot's registered area
    /// </summary>
    [HttpPost("polygon/validate-area")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<PolygonValidationResponse>>> ValidatePolygonArea(
        [FromBody] ValidatePolygonAreaRequest request)
    {
        var query = new ValidatePolygonAreaQuery
        {
            PlotId = request.PlotId,
            PolygonGeoJson = request.PolygonGeoJson,
            TolerancePercent = request.TolerancePercent ?? 10 // Default 10% (will be overridden by system setting if available)
        };

        return this.ToActionResult(await _mediator.Send(query));
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
        if (!User.TryGetUserId(out var supervisorId))
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

        return this.ToActionResult(await _mediator.Send(command));
    }

    /// <summary>
    /// Get group overview by season and year (lightweight - fast loading)
    /// If seasonId and year are not provided, returns current season
    /// </summary>
    [HttpGet("group-by-season")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<List<GroupBySeasonResponse>>>> GetGroupBySeason(
        [FromQuery] Guid? seasonId = null,
        [FromQuery] int? year = null)
    {
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(Result<List<GroupBySeasonResponse>>.Failure("User not authenticated"));
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
        if (!User.TryGetUserId(out var supervisorId))
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

    [HttpGet("available-seasons")]
    [Authorize(Roles = "Supervisor")]
    public async Task<ActionResult<Result<List<AvailableSeasonYearDto>>>> GetAvailableSeasons()
    {
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(Result<List<AvailableSeasonYearDto>>.Failure("User not authenticated"));
        }

        var query = new GetSupervisorAvailableSeasonsQuery
        {
            SupervisorId = supervisorId
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    [HttpGet("my-group")]
    [Authorize(Roles = "Supervisor")]
    [Obsolete("Use /group-by-season endpoint instead")]
    public async Task<ActionResult<Result<MyGroupResponse>>> GetMyGroup([FromQuery] Guid? seasonId = null)
    {
        if (!User.TryGetUserId(out var supervisorId))
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
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(Result<List<GroupHistorySummary>>.Failure("User not authenticated"));
        }

        var query = new GetMyGroupHistoryQuery
        {
            SupervisorId = supervisorId,
            Year = year,
            IncludeCurrentSeason = includeCurrentSeason
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Get farmers managed by supervisor (all or only assigned)
    /// </summary>
    [HttpPost("farmers")]
    [Authorize(Roles = "Supervisor")]
    [ProducesResponseType(typeof(PagedResult<List<FarmerDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<List<FarmerDTO>>>> GetFarmers(
        [FromBody] GetFarmersBySupervisorRequest request)
    {
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(PagedResult<List<FarmerDTO>>.Failure("User not authenticated"));
        }

        var query = new GetFarmersBySupervisorQuery
        {
            SupervisorId = supervisorId,
            OnlyAssigned = request.OnlyAssigned,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            SearchTerm = request.SearchTerm
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Get emergency reports for farmers managed by this supervisor (sorted by newest first)
    /// </summary>
    [HttpPost("reports")]
    [Authorize(Roles = "Supervisor")]
    [ProducesResponseType(typeof(PagedResult<List<ReportItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<List<ReportItemResponse>>>> GetReports(
        [FromBody] GetReportsBySupervisorRequest request)
    {
        if (!User.TryGetUserId(out var supervisorId))
        {
            return Unauthorized(PagedResult<List<ReportItemResponse>>.Failure("User not authenticated"));
        }

        var query = new GetReportsBySupervisorQuery
        {
            SupervisorId = supervisorId,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            SearchTerm = request.SearchTerm,
            Status = request.Status,
            Severity = request.Severity,
            ReportType = request.ReportType
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    [HttpGet("by-cluster/{clusterId:guid}")]
    [ProducesResponseType(typeof(Result<List<SupervisorDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSupervisorsByClusterId(Guid clusterId)
    {
        if (clusterId == Guid.Empty)
        {
            _logger.LogWarning("Invalid cluster ID provided: {ClusterId}", clusterId);
            return BadRequest(Result.Failure("Invalid cluster ID"));
        }

        var query = new GetSupervisorByClusterIdQuery { ClusterId = clusterId };
        var result = await _mediator.Send(query);

        if (result == null || !result.Any())
        {
            _logger.LogInformation("No supervisors found for cluster ID: {ClusterId}", clusterId);
            return NotFound(Result.Failure($"No supervisors found for cluster {clusterId}"));
        }

        return Ok(Result<List<SupervisorDTO>>.Success(
            result,
            $"Retrieved {result.Count} supervisors successfully"
        ));
    }

    [HttpGet]
    [ProducesResponseType(typeof(Result<List<SupervisorDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllSupervisor()
    {
        var query = new GetAllSupervisorQueries();
        var result = await _mediator.Send(query);

        if (result == null || !result.Any())
        {
            _logger.LogInformation("No supervisors found");
            return NotFound(Result<List<SupervisorDTO>>.Failure("No supervisors found"));
        }

        return Ok(Result<List<SupervisorDTO>>.Success(result, "Retrieved successfully"));
    }
}

public class ValidatePolygonAreaRequest
{
    public Guid PlotId { get; set; }
    public string PolygonGeoJson { get; set; } = string.Empty;
    public decimal? TolerancePercent { get; set; } // Optional, defaults to 10%
}

public class CompletePolygonRequest
{
    public string PolygonGeoJson { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class GetFarmersBySupervisorRequest
{
    public bool OnlyAssigned { get; set; } = false;
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
}

public class GetReportsBySupervisorRequest
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ReportType { get; set; }
}
