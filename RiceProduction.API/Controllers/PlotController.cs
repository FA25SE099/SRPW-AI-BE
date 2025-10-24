using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.PlotFeature.Commands.EditPlot;
using RiceProduction.Application.PlotFeature.Queries;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlotController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PlotController> _logger;

        public PlotController(IMediator mediator, ILogger<PlotController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PlotDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult<PlotDTO>> GetPlotById(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid Plot ID");
                }
                var query = new GetPlotByIDQueries(id);
                var result = await _mediator.Send(query);
                if (result == null)
                {
                    return NotFound($"Plot with id {id} not found");
                }
                return Ok(result);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting Plot {PlotId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<PlotDTO>>> GetAllPlots(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var query = new GetAllPlotQueries
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
            };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        

        [HttpPut]
        public async Task<ActionResult<Result<UpdatePlotRequest>>> EditPlot([FromBody] UpdatePlotRequest input)
        {
            var command = new EditPlotCommand
            {
                Request = input
            };
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

    }
}
