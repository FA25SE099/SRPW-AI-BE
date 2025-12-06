using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.FilterFeature.Queries.GetEnumValues;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilterController : ControllerBase
{
    private readonly IMediator _mediator;

    public FilterController(IMediator mediator)
    {
        _mediator = mediator;
    }

    
    [HttpGet("enums")]
    [ProducesResponseType(typeof(EnumValuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEnumValues([FromQuery] string? enumType = null)
    {
        var query = new GetEnumValuesQuery { EnumType = enumType };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get PlotStatus enum values
    /// </summary>
    [HttpGet("plot-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlotStatuses()
    {
        var query = new GetEnumValuesQuery { EnumType = "PlotStatus" };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data?.PlotStatuses, succeeded = true });
    }

    /// <summary>
    /// Get TaskStatus enum values
    /// </summary>
    [HttpGet("task-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaskStatuses()
    {
        var query = new GetEnumValuesQuery { EnumType = "TaskStatus" };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data?.TaskStatuses, succeeded = true });
    }

    /// <summary>
    /// Get TaskType enum values
    /// </summary>
    [HttpGet("task-types")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaskTypes()
    {
        var query = new GetEnumValuesQuery { EnumType = "TaskType" };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data?.TaskTypes, succeeded = true });
    }

    /// <summary>
    /// Get TaskPriority enum values
    /// </summary>
    [HttpGet("task-priorities")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaskPriorities()
    {
        var query = new GetEnumValuesQuery { EnumType = "TaskPriority" };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data?.TaskPriorities, succeeded = true });
    }
    [HttpGet("group-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupStatuses()
    {
        var query = new GetEnumValuesQuery { EnumType = "GroupStatus" };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data?.GroupStatuses, succeeded = true });
    }
}
