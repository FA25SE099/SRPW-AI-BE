using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Command;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Application.ReportFeature.Queries.GetReportById;

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
        try
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

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting reports");
            return StatusCode(500, "An error occurred while processing your request");
        }
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
        try
        {
            if (!_currentUser.Id.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var query = new RiceProduction.Application.ReportFeature.Queries.GetMyReports.GetMyReportsQuery
            {
                UserId = _currentUser.Id.Value,
                CurrentPage = currentPage,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Status = status,
                Severity = severity,
                ReportType = reportType
            };

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting user reports");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{reportId}")]
    [ProducesResponseType(typeof(Result<ReportItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReportById(Guid reportId)
    {
        try
        {
            var query = new GetReportByIdQuery { ReportId = reportId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateReport([FromForm] RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport.CreateEmergencyReportCommand command)
    {
        try
        {
            _logger.LogInformation(
                "Create report request: Type={AlertType}, Title={Title}, Severity={Severity}",
                command.AlertType, command.Title, command.Severity);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating report");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPost("{reportId}/resolve")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResolveReport(Guid reportId, [FromBody] ResolveReportCommand command)
    {
        try
        {
            if (reportId != command.ReportId)
            {
                return BadRequest(new { message = "Report ID in URL does not match request body" });
            }

            command.ExpertId = _currentUser.Id;

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while resolving report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}

