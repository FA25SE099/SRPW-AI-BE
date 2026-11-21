using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.SeasonFeature.Commands.CreateSeason;
using RiceProduction.Application.SeasonFeature.Commands.UpdateSeason;
using RiceProduction.Application.SeasonFeature.Commands.DeleteSeason;
using RiceProduction.Application.SeasonFeature.Queries.GetAllSeasons;
using RiceProduction.Application.SeasonFeature.Queries.GetCurrentSeason;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeasonController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SeasonController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] GetAllSeasonsQuery query)
        {
            var result = await _mediator.Send(query);
            return this.ToIActionResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSeason([FromBody] CreateSeasonCommand command)
        {
            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSeason(Guid id, [FromBody] UpdateSeasonCommand command)
        {
            if (id != command.SeasonId)
            {
                return BadRequest(new { message = "Route ID does not match command ID" });
            }

            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeason(Guid id)
        {
            var command = new DeleteSeasonCommand { SeasonId = id };
            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSeason()
        {
            var result = await _mediator.Send(new GetCurrentSeasonQuery());
            return this.ToIActionResult(result);
        }
    }
}

