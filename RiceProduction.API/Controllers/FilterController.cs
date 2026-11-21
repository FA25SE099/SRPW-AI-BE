using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
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
        var result = await _mediator.Send(new GetEnumValuesQuery { EnumType = enumType });
        return this.ToIActionResult(result);
    }

    [HttpGet("plot-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPlotStatuses() =>
        GetEnumValuesByType("PlotStatus", d => d.PlotStatuses);

    [HttpGet("task-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTaskStatuses() =>
        GetEnumValuesByType("TaskStatus", d => d.TaskStatuses);

    [HttpGet("task-types")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTaskTypes() =>
        GetEnumValuesByType("TaskType", d => d.TaskTypes);

    [HttpGet("task-priorities")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetTaskPriorities() =>
        GetEnumValuesByType("TaskPriority", d => d.TaskPriorities);

    [HttpGet("group-statuses")]
    [ProducesResponseType(typeof(List<EnumValueDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetGroupStatuses() =>
        GetEnumValuesByType("GroupStatus", d => d.GroupStatuses);

    private async Task<IActionResult> GetEnumValuesByType(string enumType, Func<EnumValuesResponse, object?> selector)
    {
        var result = await _mediator.Send(new GetEnumValuesQuery { EnumType = enumType });

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { data = result.Data != null ? selector(result.Data) : null, succeeded = true });
    }
}
