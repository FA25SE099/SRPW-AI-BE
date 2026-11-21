using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ReportResponses;
using RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;
using RiceProduction.Application.ReportFeature.Command;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Application.ReportFeature.Queries.GetContextualAISuggestions;
using RiceProduction.Application.ReportFeature.Queries.GetMyReports;
using RiceProduction.Application.ReportFeature.Queries.GetReportById;
using RiceProduction.Application.ReportFeature.Queries.GetReportWithEmergencyMaterials;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUser _currentUser;
    private readonly ILogger<ReportController> _logger;

    public ReportController(IMediator mediator, IUser currentUser, ILogger<ReportController> logger)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<List<ReportItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllReports(
        [FromQuery] int currentPage = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? reportType = null)
    {
        var query = new GetAllReportsQuery
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            SearchTerm = searchTerm,
            Status = status,
            Severity = severity,
            ReportType = reportType
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpGet("my-reports")]
    [ProducesResponseType(typeof(PagedResult<List<ReportItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] int currentPage = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? reportType = null)
    {
        if (!_currentUser.Id.HasValue)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var query = new GetMyReportsQuery
        {
            UserId = _currentUser.Id.Value,
            CurrentPage = currentPage,
            PageSize = pageSize,
            SearchTerm = searchTerm,
            Status = status,
            Severity = severity,
            ReportType = reportType
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpGet("{reportId}")]
    [ProducesResponseType(typeof(Result<ReportItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReportById(Guid reportId)
    {
        var query = new GetReportByIdQuery { ReportId = reportId };
        var result = await _mediator.Send(query);

        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateReport([FromForm] CreateEmergencyReportCommand command)
    {
        _logger.LogInformation(
            "Create report request: Type={AlertType}, Title={Title}, Severity={Severity}",
            command.AlertType, command.Title, command.Severity);

        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpPost("{reportId}/resolve")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResolveReport(Guid reportId, [FromBody] ResolveReportCommand command)
    {
        if (reportId != command.ReportId)
        {
            return BadRequest(new { message = "Report ID in URL does not match request body" });
        }

        command.ExpertId = _currentUser.Id;

        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpGet("{reportId}/emergency-materials")]
    [ProducesResponseType(typeof(Result<ReportWithEmergencyMaterialsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReportWithEmergencyMaterials(Guid reportId)
    {
        var query = new GetReportWithEmergencyMaterialsQuery { ReportId = reportId };
        var result = await _mediator.Send(query);

        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get contextual AI suggestions that analyze the existing plan and suggest specific improvements
    /// This provides incremental, apply-able suggestions rather than a complete plan
    /// </summary>
    /// <param name="reportId">The emergency report ID</param>
    /// <param name="plotCultivationId">The plot cultivation ID with existing tasks</param>
    /// <returns>Contextual AI suggestions with individual apply actions</returns>
    [HttpGet("{reportId}/contextual-suggestions")]
    [ProducesResponseType(typeof(Result<ContextualPlanSuggestions>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetContextualAISuggestions(
        Guid reportId,
        [FromQuery] Guid plotCultivationId)
    {
        var query = new GetContextualAISuggestionsQuery
        {
            ReportId = reportId,
            PlotCultivationId = plotCultivationId
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return result.Message switch
            {
                "NotFound" => NotFound(result),
                "ConfigurationError" => StatusCode(503, result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }
}


