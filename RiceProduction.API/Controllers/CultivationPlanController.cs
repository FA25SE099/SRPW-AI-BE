
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.CultivationPlanFeature.Queries;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetByPlotId;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationPlanById;

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
    public async Task<IActionResult> GetCultivationsByPlot(Guid plotId)
    {
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

}

