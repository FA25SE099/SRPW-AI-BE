using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.LateFarmerRecordFeature.Commands.CreateLateFarmerRecord;
using RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByFarmerId;
using RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateCountByPlotId;
using RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateDetailByFarmerId;
using RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLateFarmersInCluster;
using RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLatePlotsInCluster;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LateFarmerRecordController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<LateFarmerRecordController> _logger;

    public LateFarmerRecordController(IMediator mediator, ILogger<LateFarmerRecordController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a late farmer record from a cultivation task ID
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<Guid>>> CreateLateFarmerRecord([FromBody] CreateLateFarmerRecordCommand command)
    {
        try
        {
            if (command.CultivationTaskId == Guid.Empty)
            {
                return BadRequest(Result<Guid>.Failure("Invalid Cultivation Task ID"));
            }

            _logger.LogInformation("Creating late farmer record for cultivation task {TaskId}", command.CultivationTaskId);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating late farmer record");
            return StatusCode(500, Result<Guid>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get the total number of late records for a specific farmer
    /// </summary>
    [HttpGet("farmer/{farmerId}/count")]
    [ProducesResponseType(typeof(Result<FarmerLateCountDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<FarmerLateCountDTO>>> GetLateCountByFarmerId(Guid farmerId)
    {
        try
        {
            if (farmerId == Guid.Empty)
            {
                return BadRequest(Result<FarmerLateCountDTO>.Failure("Invalid Farmer ID"));
            }

            var query = new GetLateCountByFarmerIdQuery { FarmerId = farmerId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late count for farmer {FarmerId}", farmerId);
            return StatusCode(500, Result<FarmerLateCountDTO>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get detailed late records for a specific farmer including the list of all late records
    /// </summary>
    [HttpGet("farmer/{farmerId}/detail")]
    [ProducesResponseType(typeof(Result<FarmerLateDetailDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<FarmerLateDetailDTO>>> GetLateDetailByFarmerId(Guid farmerId)
    {
        try
        {
            if (farmerId == Guid.Empty)
            {
                return BadRequest(Result<FarmerLateDetailDTO>.Failure("Invalid Farmer ID"));
            }

            var query = new GetLateDetailByFarmerIdQuery { FarmerId = farmerId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late detail for farmer {FarmerId}", farmerId);
            return StatusCode(500, Result<FarmerLateDetailDTO>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get the total number of late records for a specific plot
    /// </summary>
    [HttpGet("plot/{plotId}/count")]
    [ProducesResponseType(typeof(Result<PlotLateCountDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<PlotLateCountDTO>>> GetLateCountByPlotId(Guid plotId)
    {
        try
        {
            if (plotId == Guid.Empty)
            {
                return BadRequest(Result<PlotLateCountDTO>.Failure("Invalid Plot ID"));
            }

            var query = new GetLateCountByPlotIdQuery { PlotId = plotId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting late count for plot {PlotId}", plotId);
            return StatusCode(500, Result<PlotLateCountDTO>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get list of farmers with late records in a cluster (use expertId to get all related, or supervisorId to get only from managed groups)
    /// </summary>
    [HttpGet("farmers")]
    [ProducesResponseType(typeof(PagedResult<IEnumerable<FarmerWithLateCountDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<IEnumerable<FarmerWithLateCountDTO>>>> GetLateFarmersInCluster(
        [FromQuery] Guid? agronomyExpertId = null,
        [FromQuery] Guid? supervisorId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            if (!agronomyExpertId.HasValue && !supervisorId.HasValue)
            {
                return BadRequest(PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("Either AgronomyExpertId or SupervisorId must be provided"));
            }

            var query = new GetLateFarmersInClusterQuery
            {
                AgronomyExpertId = agronomyExpertId,
                SupervisorId = supervisorId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
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
            _logger.LogError(ex, "Error occurred while getting late farmers in cluster");
            return StatusCode(500, PagedResult<IEnumerable<FarmerWithLateCountDTO>>.Failure("An error occurred while processing your request"));
        }
    }

    /// <summary>
    /// Get list of plots with late records in a cluster (use expertId to get all related, or supervisorId to get only from managed groups)
    /// </summary>
    [HttpGet("plots")]
    [ProducesResponseType(typeof(PagedResult<IEnumerable<PlotWithLateCountDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<IEnumerable<PlotWithLateCountDTO>>>> GetLatePlotsInCluster(
        [FromQuery] Guid? agronomyExpertId = null,
        [FromQuery] Guid? supervisorId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            if (!agronomyExpertId.HasValue && !supervisorId.HasValue)
            {
                return BadRequest(PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Either AgronomyExpertId or SupervisorId must be provided"));
            }

            var query = new GetLatePlotsInClusterQuery
            {
                AgronomyExpertId = agronomyExpertId,
                SupervisorId = supervisorId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
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
            _logger.LogError(ex, "Error occurred while getting late plots in cluster");
            return StatusCode(500, PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("An error occurred while processing your request"));
        }
    }
}
