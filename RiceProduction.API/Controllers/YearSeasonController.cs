using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;
using RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeason;
using RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeasonStatus;
using RiceProduction.Application.YearSeasonFeature.Commands.DeleteYearSeason;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDetail;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YearSeasonController : ControllerBase
{
    private readonly IMediator _mediator;

    public YearSeasonController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> Create([FromBody] CreateYearSeasonCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateYearSeasonCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "Route ID does not match command ID" });
        }
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateYearSeasonStatusCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "Route ID does not match command ID" });
        }
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteYearSeasonCommand { Id = id };
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("cluster/{clusterId}")]
    public async Task<IActionResult> GetByCluster(Guid clusterId, [FromQuery] int? year)
    {
        var query = new GetYearSeasonsByClusterQuery { ClusterId = clusterId, Year = year };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var query = new GetYearSeasonDetailQuery { Id = id };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}

