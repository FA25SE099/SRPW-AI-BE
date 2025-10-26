using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;
using RiceProduction.Application.StandardPlanFeature.Queries.GetStandardPlanDetail;
using RiceProduction.Application.StandardPlanFeature.Queries.ReviewStandardPlan;
using RiceProduction.Application.StandardPlanFeature.Commands.UpdateStandardPlan;

namespace RiceProduction.API.Controllers;

//[Authorize]
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

  
    [HttpGet]
    public async Task<ActionResult<Result<List<StandardPlanDto>>>> GetAll(
        [FromQuery] Guid? riceVarietyId = null,
        [FromQuery] Guid? seasonId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            _logger.LogInformation("GetAll standard plans request received");

            var query = new GetAllStandardPlansQuery
            {
                RiceVarietyId = riceVarietyId,
                SeasonId = seasonId,
                SearchTerm = searchTerm,
                IsActive = isActive
            };

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve standard plans: {Errors}", string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting standard plans");
            return StatusCode(500, Result<List<StandardPlanDto>>.Failure("An unexpected error occurred"));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<StandardPlanDetailDto>>> GetStandardPlanDetail(Guid id)
    {
        try
        {
            _logger.LogInformation("GetStandardPlanDetail request received for ID: {StandardPlanId}", id);

            var query = new GetStandardPlanDetailQuery { StandardPlanId = id };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve standard plan detail for ID {StandardPlanId}: {Errors}", 
                    id, string.Join(", ", result.Errors ?? new string[0]));
                
                
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting standard plan detail for ID: {StandardPlanId}", id);
            return StatusCode(500, Result<StandardPlanDetailDto>.Failure("An unexpected error occurred"));
        }
    }


    [HttpGet("{id:guid}/review")]

    public async Task<ActionResult<Result<StandardPlanReviewDto>>> ReviewStandardPlan(
        Guid id,
        [FromQuery] DateTime sowDate,
        [FromQuery] decimal areaInHectares)
    {
        try
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
                    id, string.Join(", ", result.Errors ?? new string[0]));


                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error occurred while reviewing standard plan ID: {StandardPlanId}", 
                id);
            return StatusCode(500, Result<StandardPlanReviewDto>.Failure(
                "An unexpected error occurred"));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> UpdateStandardPlan(
        Guid id,
        [FromBody] UpdateStandardPlanCommand command)
    {
        try
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
                    id, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error occurred while updating standard plan ID: {StandardPlanId}", 
                id);
            return StatusCode(500, Result<Guid>.Failure(
                "An unexpected error occurred"));
        }
    }
}
