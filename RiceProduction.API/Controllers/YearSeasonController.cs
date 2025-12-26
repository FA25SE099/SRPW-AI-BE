using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;
using RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeason;
using RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeasonStatus;
using RiceProduction.Application.YearSeasonFeature.Commands.DeleteYearSeason;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonsByCluster;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDetail;
using RiceProduction.Application.YearSeasonFeature.Queries.ValidateProductionPlanAgainstYearSeason;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDashboard;
using RiceProduction.Application.YearSeasonFeature.Queries.CalculateSeasonDates;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonReadiness;
using RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonFarmerSelections;
using RiceProduction.Application.YearSeasonFeature.Queries.GetGroupsByYearSeason;
using RiceProduction.Application.YearSeasonFeature.Queries.GetActiveYearSeasons;

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

    /// <summary>
    /// Get all active year seasons available for farmer cultivation selection.
    /// Returns year seasons where:
    /// - Status = PlanningOpen
    /// - AllowFarmerSelection = true
    /// - Current date is within FarmerSelectionWindow
    /// </summary>
    /// <param name="clusterId">Optional filter by cluster ID</param>
    /// <param name="year">Optional filter by year</param>
    /// <returns>List of year seasons available for farmer selection</returns>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveYearSeasons([FromQuery] Guid? clusterId, [FromQuery] int? year)
    {
        var query = new GetActiveYearSeasonsQuery 
        { 
            ClusterId = clusterId, 
            Year = year 
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
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

    [HttpPut("{id:guid}")]
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

    [HttpPatch("{id:guid}/status")]
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

    [HttpDelete("{id:guid}")]
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

    [HttpGet("cluster/{clusterId:guid}")]
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

    [HttpGet("{id:guid}")]
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

    /// <summary>
    /// Get comprehensive dashboard for a YearSeason including groups, plans, materials, and timeline
    /// </summary>
    [HttpGet("{id:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid id)
    {
        var query = new GetYearSeasonDashboardQuery { YearSeasonId = id };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Validate if a production plan can be created for a group based on YearSeason constraints
    /// </summary>
    [HttpPost("validate-production-plan")]
    public async Task<IActionResult> ValidateProductionPlan([FromBody] ValidateProductionPlanAgainstYearSeasonQuery query)
    {
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Calculate actual season dates based on Season's DD/MM format and a specific year.
    /// Useful for auto-populating start/end dates when creating a YearSeason.
    /// </summary>
    /// <param name="seasonId">Season ID to get base dates from</param>
    /// <param name="year">Year to apply to the season dates</param>
    /// <returns>Calculated dates with suggestions for selection and planting windows</returns>
    [HttpGet("calculate-dates")]
    public async Task<IActionResult> CalculateSeasonDates([FromQuery] Guid seasonId, [FromQuery] int year)
    {
        var query = new CalculateSeasonDatesQuery 
        { 
            SeasonId = seasonId, 
            Year = year 
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Get readiness information for a YearSeason.
    /// Shows whether the cluster is ready to form groups or if groups already exist.
    /// </summary>
    /// <param name="id">YearSeason ID</param>
    /// <returns>Readiness information including blocking issues and recommendations</returns>
    [HttpGet("{id:guid}/readiness")]
    public async Task<IActionResult> GetReadiness(Guid id)
    {
        var query = new GetYearSeasonReadinessQuery { YearSeasonId = id };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Get farmer rice variety selection status for a YearSeason.
    /// Shows which farmers have selected varieties and which are pending.
    /// </summary>
    /// <param name="id">YearSeason ID</param>
    /// <returns>Farmer selection status including variety breakdown and pending farmers</returns>
    [HttpGet("{id:guid}/farmer-selections")]
    public async Task<IActionResult> GetFarmerSelections(Guid id)
    {
        var query = new GetYearSeasonFarmerSelectionsQuery { YearSeasonId = id };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Get all groups for a specific YearSeason.
    /// Returns comprehensive group information including plots, farmers, production plans, and status summary.
    /// </summary>
    /// <param name="id">YearSeason ID</param>
    /// <returns>List of groups with detailed information and status summary</returns>
    [HttpGet("{id:guid}/groups")]
    public async Task<IActionResult> GetGroupsByYearSeason(Guid id)
    {
        var query = new GetGroupsByYearSeasonQuery { YearSeasonId = id };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}

