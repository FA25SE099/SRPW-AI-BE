using MediatR;
using Microsoft.AspNetCore.Mvc;
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
[Route("api/production-plans")] // Định nghĩa route cho Controller này
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
        // Gửi Command đến Mediator để xử lý
        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            // Trả về BadRequest nếu logic nghiệp vụ hoặc validation thất bại
            return BadRequest(result);
        }

        // Trả về OK cùng với ID của Plan vừa tạo
        return Ok(result);
    }

    // Bạn có thể thêm các endpoint khác như GetById, GetAll, Update, Delete tại đây
    [HttpGet("draft")]
    [ProducesResponseType(typeof(Result<GeneratePlanDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GeneratePlanDraftResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDraft([FromQuery] GeneratePlanDraftQuery query)
    {
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedProductionPlans(
        [FromQuery] Guid? groupId,
        [FromQuery] Guid? supervisorId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in GetApprovedProductionPlans endpoint");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edit([FromBody] EditPlanCommand command)
    {
        try
        {

            _logger.LogInformation("EditProductionPlan request received for ID: {PlanId}", command.PlanId);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to edit production plan ID {PlanId}: {Errors}",
                    command.PlanId, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation("Successfully edited production plan with ID: {PlanId}", command.PlanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while editing production plan ID: {PlanId}", command.PlanId);
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitPlanCommand command)
    {
        try
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
                    id, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation("Successfully submitted production plan with ID: {PlanId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while submitting production plan ID: {PlanId}", id);
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpPost("{id:guid}/approve-reject")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveReject(Guid id, [FromBody] ApproveRejectPlanCommand command)
    {
        try
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
                    id, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation("Successfully {Action} production plan with ID: {PlanId}", 
                command.Approved ? "approved" : "rejected", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while approving/rejecting production plan ID: {PlanId}", id);
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpGet("pending-approvals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPendingApprovals()
    {
        try
        {
            _logger.LogInformation("GetPendingApprovals request received");

            var query = new GetPendingApprovalsQuery();
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve pending approvals: {Errors}", 
                    string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved pending approvals");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting pending approvals");
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    [HttpPost("emergency")]
    public async Task<IActionResult> GetEmergencyPlans([FromBody] GetEmergencyPlansQuery query)
    {
        try
        {
            _logger.LogInformation(
                "GetEmergencyPlans request received - Page: {Page}, PageSize: {PageSize}",
                query.CurrentPage, query.PageSize);

            //var query = new GetEmergencyPlansQuery
            //{
            //    CurrentPage = currentPage,
            //    PageSize = pageSize,
            //    GroupId = groupId,
            //    ClusterId = clusterId,
            //    SearchTerm = searchTerm
            //};

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to retrieve emergency plans: {Errors}",
                    string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved emergency plans");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting emergency plans");
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    //[HttpPost("resolve-emergency")]
    //public async Task<IActionResult> ResolveEmergency([FromBody] ResolveEmergencyPlanCommand1 command)
    //{
    //    try
    //    {

    //        _logger.LogInformation(
    //            "ResolveEmergencyPlan request received for ID: {PlanId}, NewVersion: {VersionName}",
    //            command.PlanId, command.NewVersionName);

    //        var result = await _mediator.Send(command);

    //        if (!result.Succeeded)
    //        {
    //            _logger.LogWarning(
    //                "Failed to resolve emergency plan ID {PlanId}: {Errors}",
    //                command.PlanId, string.Join(", ", result.Errors ?? new string[0]));

    //            return BadRequest(result);
    //        }

    //        _logger.LogInformation(
    //            "Successfully resolved emergency plan ID {PlanId} with new version '{VersionName}'",
    //            command.PlanId, command.NewVersionName);
    //        return Ok(result);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex,
    //            "Unexpected error occurred while resolving emergency plan ID: {PlanId}",
    //            command.PlanId);
    //        return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
    //    }
    //}

    /// <summary>
    /// Creates an emergency plan for a single plot (not the entire group)
    /// </summary>
    [HttpPost("emergency-plan-for-plot")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEmergencyPlanForPlot([FromBody] CreateEmergencyPlanForPlotCommand command)
    {
        try
        {
            _logger.LogInformation(
                "CreateEmergencyPlanForPlot request received for Plot: {PlotId}, Report: {ReportId}, NewVersion: {VersionName}",
                command.PlotId, command.EmergencyReportId, command.NewVersionName);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to create emergency plan for Plot {PlotId}: {Errors}",
                    command.PlotId, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation(
                "Successfully created emergency plan for Plot {PlotId} with new version '{VersionName}'",
                command.PlotId, command.NewVersionName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error occurred while creating emergency plan for Plot: {PlotId}",
                command.PlotId);
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        try
        {
            _logger.LogInformation("GetPlanDetail request received for ID: {PlanId}", id);

            var query = new GetPlanDetailsForExpertQuery { PlanId = id };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve plan detail for ID {PlanId}: {Errors}",
                    id, string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved plan detail for ID: {PlanId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting plan detail for ID: {PlanId}", id);
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id:guid}/execution-summary")]
    [ProducesResponseType(typeof(Result<PlanExecutionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionSummary(Guid id)
    {
        try
        {
            _logger.LogInformation("GetPlanExecutionSummary request received for ID: {PlanId}", id);

            var query = new GetPlanExecutionSummaryQuery { PlanId = id };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve execution summary for ID {PlanId}: {Errors}",
                    id, string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved execution summary for ID: {PlanId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting execution summary for ID: {PlanId}", id);
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    [HttpGet("{id:guid}/cultivation-tasks")]
    [ProducesResponseType(typeof(Result<List<CultivationTaskSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCultivationTasks(
        Guid id,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] Guid? plotId = null)
    {
        try
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
                    id, string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved cultivation tasks for Plan ID: {PlanId}", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting cultivation tasks for Plan ID: {PlanId}", id);
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }

    [HttpGet("plot-implementation")]
    [ProducesResponseType(typeof(Result<PlotImplementationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlotImplementation(
        [FromQuery] Guid plotId,
        [FromQuery] Guid productionPlanId)
    {
        try
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
                    plotId, productionPlanId, string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully retrieved plot implementation for Plot ID: {PlotId}, Plan ID: {PlanId}",
                plotId, productionPlanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting plot implementation for Plot ID: {PlotId}, Plan ID: {PlanId}",
                plotId, productionPlanId);
            return StatusCode(500, new { message = "An unexpected error occurred" });
        }
    }
}