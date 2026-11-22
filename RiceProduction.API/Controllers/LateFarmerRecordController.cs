using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
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
        if (command.CultivationTaskId == Guid.Empty)
        {
            return BadRequest(Result<Guid>.Failure("Invalid Cultivation Task ID"));
        }

        _logger.LogInformation("Creating late farmer record for cultivation task {TaskId}", command.CultivationTaskId);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Get the total number of late records for a specific farmer
    /// </summary>
    [HttpGet("farmer/{farmerId}/count")]
    [ProducesResponseType(typeof(Result<FarmerLateCountDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Result<FarmerLateCountDTO>>> GetLateCountByFarmerId(Guid farmerId) =>
        ExecuteByIdQueryAsync(
            farmerId,
            "Invalid Farmer ID",
            new GetLateCountByFarmerIdQuery { FarmerId = farmerId });

    /// <summary>
    /// Get detailed late records for a specific farmer including the list of all late records
    /// </summary>
    [HttpGet("farmer/{farmerId}/detail")]
    [ProducesResponseType(typeof(Result<FarmerLateDetailDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Result<FarmerLateDetailDTO>>> GetLateDetailByFarmerId(Guid farmerId) =>
        ExecuteByIdQueryAsync(
            farmerId,
            "Invalid Farmer ID",
            new GetLateDetailByFarmerIdQuery { FarmerId = farmerId });

    /// <summary>
    /// Get the total number of late records for a specific plot
    /// </summary>
    [HttpGet("plot/{plotId}/count")]
    [ProducesResponseType(typeof(Result<PlotLateCountDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<Result<PlotLateCountDTO>>> GetLateCountByPlotId(Guid plotId) =>
        ExecuteByIdQueryAsync(
            plotId,
            "Invalid Plot ID",
            new GetLateCountByPlotIdQuery { PlotId = plotId });

    /// <summary>
    /// Get list of farmers with late records in a cluster (use expertId to get all related, or supervisorId to get only from managed groups)
    /// </summary>
    [HttpGet("farmers")]
    [ProducesResponseType(typeof(PagedResult<IEnumerable<FarmerWithLateCountDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<PagedResult<IEnumerable<FarmerWithLateCountDTO>>>> GetLateFarmersInCluster(
        [FromQuery] Guid? agronomyExpertId = null,
        [FromQuery] Guid? supervisorId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null) =>
        ExecuteClusterPagedQueryAsync(
            agronomyExpertId,
            supervisorId,
            new GetLateFarmersInClusterQuery
            {
                AgronomyExpertId = agronomyExpertId,
                SupervisorId = supervisorId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
            });

    /// <summary>
    /// Get list of plots with late records in a cluster (use expertId to get all related, or supervisorId to get only from managed groups)
    /// </summary>
    [HttpGet("plots")]
    [ProducesResponseType(typeof(PagedResult<IEnumerable<PlotWithLateCountDTO>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<PagedResult<IEnumerable<PlotWithLateCountDTO>>>> GetLatePlotsInCluster(
        [FromQuery] Guid? agronomyExpertId = null,
        [FromQuery] Guid? supervisorId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null) =>
        ExecuteClusterPagedQueryAsync(
            agronomyExpertId,
            supervisorId,
            new GetLatePlotsInClusterQuery
            {
                AgronomyExpertId = agronomyExpertId,
                SupervisorId = supervisorId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
            });

    private async Task<ActionResult<Result<T>>> ExecuteByIdQueryAsync<T>(
        Guid id,
        string invalidIdMessage,
        IRequest<Result<T>> query)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(Result<T>.Failure(invalidIdMessage));
        }

        var result = await _mediator.Send(query);
        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    private async Task<ActionResult<PagedResult<T>>> ExecuteClusterPagedQueryAsync<T>(
        Guid? agronomyExpertId,
        Guid? supervisorId,
        IRequest<PagedResult<T>> query)
    {
        if (!agronomyExpertId.HasValue && !supervisorId.HasValue)
        {
            return BadRequest(PagedResult<T>.Failure("Either AgronomyExpertId or SupervisorId must be provided"));
        }

        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }
}
