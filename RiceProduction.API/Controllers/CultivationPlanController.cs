using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationPlanFeature.Queries;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationPlanById;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCurrentPlotCultivation;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetPlotCultivationByGroupAndPlot;

[ApiController]
[Route("api/cultivation-plan")]
public class CultivationPlanController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUser _currentUser;

    public CultivationPlanController(IMediator mediator, IUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("plan-view/{plotCultivationId}")]
    public async Task<IActionResult> GetFarmerPlanView(Guid plotCultivationId)
    {  
        var query = new GetFarmerPlanViewQuery { PlotCultivationId = plotCultivationId };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    
    [HttpGet("by-plot/{plotId}")]
    [Authorize] // Require authentication
    public async Task<IActionResult> GetCultivationsByPlot(Guid plotId)
    {
        if (_currentUser.Id == null)
        {
            return Unauthorized(Result<object>.Failure("User not authenticated", "AuthenticationRequired"));
        }
        var query = new GetCultivationsForPlotQuery { PlotId = plotId, FarmerId = (Guid)_currentUser.Id };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    
    [HttpGet("{planId}")]
    public async Task<IActionResult> GetCultivationPlanById(Guid planId)
    {
        var query = new GetCultivationPlanByIdQuery { PlanId = planId };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("current/{plotId}")]
    public async Task<IActionResult> GetCurrentPlotCultivation(Guid plotId)
    {
        var query = new GetCurrentPlotCultivationQuery { PlotId = plotId };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost("by-group-plot")]
    public async Task<IActionResult> GetCurrentPlotCultivationByGroup([FromBody] GetPlotCultivationByGroupAndPlotQuery query)
    {
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}

