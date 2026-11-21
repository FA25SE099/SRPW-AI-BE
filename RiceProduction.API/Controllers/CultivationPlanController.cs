using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
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
        var result = await _mediator.Send(new GetFarmerPlanViewQuery { PlotCultivationId = plotCultivationId });
        return this.ToIActionResult(result);
    }

    [HttpGet("by-plot/{plotId}")]
    [Authorize]
    public async Task<IActionResult> GetCultivationsByPlot(Guid plotId)
    {
        if (!_currentUser.Id.HasValue)
        {
            return Unauthorized(Result<object>.Failure("User not authenticated", "AuthenticationRequired"));
        }

        var query = new GetCultivationsForPlotQuery
        {
            PlotId = plotId,
            FarmerId = _currentUser.Id.Value
        };

        var result = await _mediator.Send(query);
        return this.ToIActionResult(result);
    }

    [HttpGet("{planId}")]
    public async Task<IActionResult> GetCultivationPlanById(Guid planId)
    {
        var result = await _mediator.Send(new GetCultivationPlanByIdQuery { PlanId = planId });
        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    [HttpGet("current/{plotId}")]
    public async Task<IActionResult> GetCurrentPlotCultivation(Guid plotId)
    {
        var result = await _mediator.Send(new GetCurrentPlotCultivationQuery { PlotId = plotId });
        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    [HttpPost("by-group-plot")]
    public async Task<IActionResult> GetCurrentPlotCultivationByGroup([FromBody] GetPlotCultivationByGroupAndPlotQuery query)
    {
        var result = await _mediator.Send(query);
        return result.Succeeded ? Ok(result) : NotFound(result);
    }
}

