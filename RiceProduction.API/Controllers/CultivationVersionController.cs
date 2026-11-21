using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationVersionFeature.Queries.GetVersionsByPlotCultivation;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/cultivation-version")]
public class CultivationVersionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CultivationVersionController> _logger;

    public CultivationVersionController(IMediator mediator, ILogger<CultivationVersionController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("by-plot-cultivation/{plotCultivationId}")]
    [ProducesResponseType(typeof(Result<List<CultivationVersionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetVersionsByPlotCultivation(Guid plotCultivationId)
    {
        var query = new GetVersionsByPlotCultivationQuery { PlotCultivationId = plotCultivationId };
        var result = await _mediator.Send(query);

        return result.Succeeded ? Ok(result) : this.ToNotFoundOrBadRequest(result);
    }
}
