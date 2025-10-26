using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.ProductionPlanFeature.Commands.CreateProductionPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GeneratePlanDraft;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetApproved;
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
}