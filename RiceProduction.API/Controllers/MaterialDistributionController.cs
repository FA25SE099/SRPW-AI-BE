using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.MaterialDistributionFeature.Commands.BulkConfirmMaterialDistribution;
using RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialDistribution;
using RiceProduction.Application.MaterialDistributionFeature.Commands.ConfirmMaterialReceipt;
using RiceProduction.Application.MaterialDistributionFeature.Commands.InitiateMaterialDistribution;
using RiceProduction.Application.MaterialDistributionFeature.Queries.GetMaterialDistributionsForGroup;

namespace RiceProduction.API.Controllers;

/// <summary>
/// Material Distribution Controller - Manages material distribution to farmers
/// Handles supervisor distribution confirmation and farmer receipt confirmation
/// </summary>
[ApiController]
[Route("api/material-distribution")]
public class MaterialDistributionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MaterialDistributionController> _logger;

    public MaterialDistributionController(
        IMediator mediator,
        ILogger<MaterialDistributionController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get material distributions grouped by farmer/plot for better UI display
    /// Shows all materials for each farmer in a single card - RECOMMENDED for frontend
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <returns>Distributions grouped by farmer with all their materials</returns>
    [HttpGet("group/{groupId}/grouped")]
    [ProducesResponseType(typeof(GroupedMaterialDistributionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForGroupGrouped(Guid groupId)
    {
        try
        {
            var query = new GetMaterialDistributionsGroupedByPlotQuery { GroupId = groupId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to get grouped distributions for group {GroupId}: {Message}", 
                    groupId, result.Message);
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grouped distributions for group {GroupId}", groupId);
            return StatusCode(500, new { message = "An error occurred while retrieving grouped distributions" });
        }
    }

    /// <summary>
    /// Get all material distributions for a specific group (flat list)
    /// Use /grouped endpoint for better UI display
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <returns>List of material distributions with status counts</returns>
    [HttpGet("group/{groupId}")]
    [ProducesResponseType(typeof(MaterialDistributionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForGroup(Guid groupId)
    {
        try
        {
            var query = new GetMaterialDistributionsForGroupQuery { GroupId = groupId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to get distributions for group {GroupId}: {Message}", 
                    groupId, result.Message);
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distributions for group {GroupId}", groupId);
            return StatusCode(500, new { message = "An error occurred while retrieving distributions" });
        }
    }

    /// <summary>
    /// Manually initiate material distributions for a production plan
    /// NOTE: This is optional - distributions are automatically created when a plan is approved
    /// </summary>
    /// <param name="command">Distribution initiation details</param>
    /// <returns>Created distributions</returns>
    [HttpPost("initiate")]
    [Authorize(Roles = "Supervisor,Expert,Admin")]
    [ProducesResponseType(typeof(InitiateMaterialDistributionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateDistribution([FromBody] InitiateMaterialDistributionCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to initiate distributions for group {GroupId}: {Message}", 
                    command.GroupId, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully initiated {Count} distributions for group {GroupId}", 
                result.Data?.DistributionsCreated, command.GroupId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating distributions for group {GroupId}", command.GroupId);
            return StatusCode(500, new { message = "An error occurred while initiating distributions" });
        }
    }

    /// <summary>
    /// Bulk confirm ALL material distributions for a plot cultivation at once
    /// Supervisor provides all materials for the entire plan and confirms with images
    /// Recommended: Use this for bulk confirmation (e.g., 10 materials = 1 confirmation)
    /// </summary>
    /// <param name="command">Bulk confirmation details including images for all materials</param>
    /// <returns>Number of distributions confirmed</returns>
    [HttpPost("confirm-bulk")]
    [Authorize(Roles = "Supervisor,Admin")]
    [ProducesResponseType(typeof(BulkConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkConfirmDistribution([FromBody] BulkConfirmMaterialDistributionCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Supervisor {SupervisorId} failed to bulk confirm distributions for PlotCultivation {PlotCultivationId}: {Message}",
                    command.SupervisorId, command.PlotCultivationId, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation(
                "Supervisor {SupervisorId} bulk confirmed {Count} distributions for PlotCultivation {PlotCultivationId}",
                command.SupervisorId, result.Data?.TotalDistributionsConfirmed, command.PlotCultivationId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error bulk confirming distributions for PlotCultivation {PlotCultivationId} by supervisor {SupervisorId}",
                command.PlotCultivationId, command.SupervisorId);
            return StatusCode(500, new { message = "An error occurred while bulk confirming distributions" });
        }
    }

    /// <summary>
    /// Supervisor confirms single material distribution with proof images
    /// Note: Consider using confirm-bulk endpoint for better UX
    /// </summary>
    /// <param name="command">Confirmation details including images and notes</param>
    /// <returns>Success status</returns>
    [HttpPost("confirm")]
    [Authorize(Roles = "Supervisor,Admin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmDistribution([FromBody] ConfirmMaterialDistributionCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Supervisor {SupervisorId} failed to confirm distribution {DistributionId}: {Message}",
                    command.SupervisorId, command.MaterialDistributionId, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation(
                "Supervisor {SupervisorId} confirmed distribution {DistributionId} on {Date}",
                command.SupervisorId, command.MaterialDistributionId, command.ActualDistributionDate);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error confirming distribution {DistributionId} by supervisor {SupervisorId}",
                command.MaterialDistributionId, command.SupervisorId);
            return StatusCode(500, new { message = "An error occurred while confirming distribution" });
        }
    }

    /// <summary>
    /// Farmer confirms receipt of materials
    /// </summary>
    /// <param name="command">Receipt confirmation details</param>
    /// <returns>Success status</returns>
    [HttpPost("confirm-receipt")]
    [Authorize(Roles = "Farmer,Admin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmReceipt([FromBody] ConfirmMaterialReceiptCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Farmer {FarmerId} failed to confirm receipt for distribution {DistributionId}: {Message}",
                    command.FarmerId, command.MaterialDistributionId, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation(
                "Farmer {FarmerId} confirmed receipt for distribution {DistributionId}",
                command.FarmerId, command.MaterialDistributionId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error confirming receipt for distribution {DistributionId} by farmer {FarmerId}",
                command.MaterialDistributionId, command.FarmerId);
            return StatusCode(500, new { message = "An error occurred while confirming receipt" });
        }
    }

    /// <summary>
    /// Get material distributions for a specific farmer (for mobile app)
    /// </summary>
    /// <param name="farmerId">The farmer ID</param>
    /// <returns>List of distributions for the farmer</returns>
    [HttpGet("farmer/{farmerId}")]
    [Authorize(Roles = "Farmer,Admin")]
    [ProducesResponseType(typeof(List<MaterialDistributionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForFarmer(Guid farmerId)
    {
        try
        {
            // TODO: Create GetMaterialDistributionsForFarmerQuery if needed
            // For now, return a message indicating this needs to be implemented
            _logger.LogInformation("Farmer {FarmerId} requested their distributions", farmerId);
            
            return Ok(new 
            { 
                message = "Farmer-specific endpoint - use group endpoint and filter on frontend",
                farmerId = farmerId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distributions for farmer {FarmerId}", farmerId);
            return StatusCode(500, new { message = "An error occurred while retrieving distributions" });
        }
    }

    /// <summary>
    /// Get a specific material distribution by ID
    /// </summary>
    /// <param name="distributionId">The distribution ID</param>
    /// <returns>Distribution details</returns>
    [HttpGet("{distributionId}")]
    [ProducesResponseType(typeof(MaterialDistributionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid distributionId)
    {
        try
        {
            // TODO: Create GetMaterialDistributionByIdQuery if needed
            _logger.LogInformation("Requested distribution {DistributionId}", distributionId);
            
            return Ok(new 
            { 
                message = "Get by ID endpoint - use group endpoint and filter on frontend",
                distributionId = distributionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distribution {DistributionId}", distributionId);
            return StatusCode(500, new { message = "An error occurred while retrieving distribution" });
        }
    }

    /// <summary>
    /// Get overdue distributions (for notifications/alerts)
    /// </summary>
    /// <returns>List of overdue distributions</returns>
    [HttpGet("overdue")]
    [Authorize(Roles = "Supervisor,Expert,Admin")]
    [ProducesResponseType(typeof(List<MaterialDistributionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue()
    {
        try
        {
            // TODO: Create GetOverdueMaterialDistributionsQuery if needed
            _logger.LogInformation("Requested overdue distributions");
            
            return Ok(new 
            { 
                message = "Overdue endpoint - use group endpoint and filter isOverdue on frontend"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue distributions");
            return StatusCode(500, new { message = "An error occurred while retrieving overdue distributions" });
        }
    }
}

