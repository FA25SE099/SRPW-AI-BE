using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;
using RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;
using RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;
using RiceProduction.Application.ProductionPlanFeature.Commands.SubmitPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GeneratePlanDraft;
using RiceProduction.Application.ReportFeature.Command;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetApproved;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetCultivationTasksByPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetEmergencyPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanDetail;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanExecutionSummary;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPlotImplementation;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;
using RiceProduction.Application.ProductionPlanFeature.Commands.ResolveEmergencyPlan;
namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/production-plans")]
public class ProductionPlanController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProductionPlanController> _logger;
    public ProductionPlanController(IMediator mediator, ILogger<ProductionPlanController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new Production Plan along with its stages, tasks, and material specifications.
    /// The TotalArea for cost calculation is determined either from the linked Group or the command payload.
    /// </summary>
    /// <param name="command">The command containing the plan details.</param>
    /// <returns>The ID (Guid) of the newly created Production Plan.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductionPlanCommand command)
    {
        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpGet("draft")]
    [ProducesResponseType(typeof(Result<GeneratePlanDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GeneratePlanDraftResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDraft([FromQuery] GeneratePlanDraftQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedProductionPlans(
        [FromQuery] Guid? groupId,
        [FromQuery] Guid? supervisorId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        _logger.LogInformation("Received request to get approved production plans");
        var query = new GetApprovedQueries
        {
            GroupId = groupId,
            SupervisorId = supervisorId,
            FromDate = fromDate,
            ToDate = toDate
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve approved production plans: {Message}", result.Message);
            return BadRequest(result);
        }
        _logger.LogInformation("Successfully retrieved approved production plans");
        return Ok(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edit([FromBody] EditPlanCommand command)
    {
        _logger.LogInformation("EditProductionPlan request received for ID: {PlanId}", command.PlanId);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to edit production plan ID {PlanId}: {Errors}",
                command.PlanId, FormatErrors(result.Errors));

            return BadRequest(result);
        }

        _logger.LogInformation("Successfully edited production plan with ID: {PlanId}", command.PlanId);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitPlanCommand command)
    {
        if (id != command.PlanId)
        {
            return BadRequest(Result<Guid>.Failure(
                "Route ID does not match command PlanId.",
                "IdMismatch"));
        }

        _logger.LogInformation("SubmitProductionPlan request received for ID: {PlanId}", id);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to submit production plan ID {PlanId}: {Errors}",
                id, FormatErrors(result.Errors));

            return BadRequest(result);
        }

        _logger.LogInformation("Successfully submitted production plan with ID: {PlanId}", id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve-reject")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveReject(Guid id, [FromBody] ApproveRejectPlanCommand command)
    {
        if (id != command.PlanId)
        {
            return BadRequest(Result<Guid>.Failure(
                "Route ID does not match command PlanId.",
                "IdMismatch"));
        }

        _logger.LogInformation("ApproveRejectProductionPlan request received for ID: {PlanId}, Approved: {Approved}",
            id, command.Approved);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to approve/reject production plan ID {PlanId}: {Errors}",
                id, FormatErrors(result.Errors));

            return BadRequest(result);
        }

        _logger.LogInformation("Successfully {Action} production plan with ID: {PlanId}",
            command.Approved ? "approved" : "rejected", id);
        return Ok(result);
    }

    [HttpGet("pending-approvals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPendingApprovals()
    {
        _logger.LogInformation("GetPendingApprovals request received");

        var query = new GetPendingApprovalsQuery();
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve pending approvals: {Errors}",
                FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved pending approvals");
        return Ok(result);
    }

    [HttpPost("emergency")]
    public async Task<IActionResult> GetEmergencyPlans([FromBody] GetEmergencyPlansQuery query)
    {
        _logger.LogInformation(
            "GetEmergencyPlans request received - Page: {Page}, PageSize: {PageSize}",
            query.CurrentPage, query.PageSize);

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to retrieve emergency plans: {Errors}",
                FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved emergency plans");
        return Ok(result);
    }

    /// <summary>
    /// Creates an emergency plan for a single plot (not the entire group)
    /// </summary>
    [HttpPost("emergency-plan-for-plot")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEmergencyPlanForPlot([FromBody] CreateEmergencyPlanForPlotCommand command)
    {
        _logger.LogInformation(
            "CreateEmergencyPlanForPlot request received for Plot: {PlotId}, Report: {ReportId}, NewVersion: {VersionName}",
            command.PlotId, command.EmergencyReportId, command.NewVersionName);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to create emergency plan for Plot {PlotId}: {Errors}",
                command.PlotId, FormatErrors(result.Errors));

            return BadRequest(result);
        }

        _logger.LogInformation(
            "Successfully created emergency plan for Plot {PlotId} with new version '{VersionName}'",
            command.PlotId, command.NewVersionName);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        _logger.LogInformation("GetPlanDetail request received for ID: {PlanId}", id);

        var query = new GetPlanDetailsForExpertQuery { PlanId = id };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve plan detail for ID {PlanId}: {Errors}",
                id, FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved plan detail for ID: {PlanId}", id);
        return Ok(result);
    }

    [HttpGet("{id:guid}/execution-summary")]
    [ProducesResponseType(typeof(Result<PlanExecutionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionSummary(Guid id)
    {
        _logger.LogInformation("GetPlanExecutionSummary request received for ID: {PlanId}", id);

        var query = new GetPlanExecutionSummaryQuery { PlanId = id };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve execution summary for ID {PlanId}: {Errors}",
                id, FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved execution summary for ID: {PlanId}", id);
        return Ok(result);
    }

    [HttpGet("{id:guid}/cultivation-tasks")]
    [ProducesResponseType(typeof(Result<List<CultivationTaskSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCultivationTasks(
        Guid id,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] Guid? plotId = null)
    {
        _logger.LogInformation("GetCultivationTasksByPlan request received for Plan ID: {PlanId}", id);

        var query = new GetCultivationTasksByPlanQuery
        {
            ProductionPlanId = id,
            StatusFilter = status,
            PlotFilter = plotId
        };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve cultivation tasks for Plan ID {PlanId}: {Errors}",
                id, FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved cultivation tasks for Plan ID: {PlanId}", id);
        return Ok(result);
    }

    [HttpGet("plot-implementation")]
    [ProducesResponseType(typeof(Result<PlotImplementationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlotImplementation(
        [FromQuery] Guid plotId,
        [FromQuery] Guid productionPlanId)
    {
        _logger.LogInformation("GetPlotImplementation request received for Plot ID: {PlotId}, Plan ID: {PlanId}",
            plotId, productionPlanId);

        var query = new GetPlotImplementationQuery
        {
            PlotId = plotId,
            ProductionPlanId = productionPlanId
        };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve plot implementation for Plot ID {PlotId}, Plan ID {PlanId}: {Errors}",
                plotId, productionPlanId, FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully retrieved plot implementation for Plot ID: {PlotId}, Plan ID: {PlanId}",
            plotId, productionPlanId);
        return Ok(result);
    }

    private static string FormatErrors(IEnumerable<string>? errors) =>
        string.Join(", ", errors ?? Array.Empty<string>());
}