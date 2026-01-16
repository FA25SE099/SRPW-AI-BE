using MediatR;
using Microsoft.AspNetCore.Mvc;
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
        try
        {
            var query = new GetVersionsByPlotCultivationQuery { PlotCultivationId = plotCultivationId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting versions for PlotCultivation {PlotCultivationId}", plotCultivationId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
