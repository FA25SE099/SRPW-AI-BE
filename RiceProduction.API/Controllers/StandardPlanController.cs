using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;
using RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;
using RiceProduction.Application.StandardPlanFeature.Queries.ReviewStandardPlan;
using RiceProduction.Application.StandardPlanFeature.Commands.CreateStandardPlan;
using RiceProduction.Application.StandardPlanFeature.Commands.UpdateStandardPlan;
using RiceProduction.Application.StandardPlanFeature.Queries.DownloadAllStandardPlansExcel;
using RiceProduction.Application.StandardPlanFeature.Queries.DownloadStandardPlanSampleExcel;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandardPlanController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StandardPlanController> _logger;

    public StandardPlanController(IMediator mediator, ILogger<StandardPlanController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new Standard Plan template with stages, tasks, and materials.
    /// Only AgronomyExperts can create standard plans.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateStandardPlanCommand command)
    {
        _logger.LogInformation("Create StandardPlan request received: {PlanName}", command.PlanName);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to create standard plan: {Errors}", FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully created standard plan with ID: {StandardPlanId}", result.Data);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<Result<List<StandardPlanDto>>>> GetAll(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null)
    {
        _logger.LogInformation("GetAll standard plans request received");

        var query = new GetAllStandardPlansQuery
        {
            CategoryId = categoryId,
            SearchTerm = searchTerm,
            IsActive = isActive
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve standard plans: {Errors}", FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<StandardPlanDetailDto>>> GetStandardPlanDetail(Guid id)
    {
        _logger.LogInformation("GetStandardPlanDetail request received for ID: {StandardPlanId}", id);

        var query = new GetStandardPlanDetailQuery { StandardPlanId = id };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve standard plan detail for ID {StandardPlanId}: {Errors}",
                id, FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}/review")]
    public async Task<ActionResult<Result<StandardPlanReviewDto>>> ReviewStandardPlan(
        Guid id,
        [FromQuery] DateTime sowDate,
        [FromQuery] decimal areaInHectares)
    {
        _logger.LogInformation(
            "ReviewStandardPlan request received for ID: {StandardPlanId}, SowDate: {SowDate}, Area: {Area}",
            id, sowDate, areaInHectares);

        var query = new ReviewStandardPlanQuery
        {
            StandardPlanId = id,
            SowDate = sowDate,
            AreaInHectares = areaInHectares
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to review standard plan ID {StandardPlanId}: {Errors}",
                id, FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> UpdateStandardPlan(
        Guid id,
        [FromBody] UpdateStandardPlanCommand command)
    {
        if (id != command.StandardPlanId)
        {
            return BadRequest(Result<Guid>.Failure(
                "Route ID does not match command ID.",
                "IdMismatch"));
        }

        _logger.LogInformation("UpdateStandardPlan request received for ID: {StandardPlanId}", id);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to update standard plan ID {StandardPlanId}: {Errors}",
                id, FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpPost("download-excel")]
    public async Task<IActionResult> DownloadStandardPlansExcel([FromBody] DownloadAllStandardPlansExcelQuery query)
    {
        return this.ToFileActionResult(await _mediator.Send(query));
    }

    [HttpGet("download-sample-excel")]
    public async Task<IActionResult> DownloadSampleExcel()
    {
        return this.ToFileActionResult(await _mediator.Send(new DownloadStandardPlanSampleExcelQuery()));
    }

    private static string FormatErrors(IEnumerable<string>? errors) =>
        string.Join(", ", errors ?? Array.Empty<string>());
}
