using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.SeasonFeature.Commands.CreateSeason;
using RiceProduction.Application.SeasonFeature.Commands.UpdateSeason;
using RiceProduction.Application.SeasonFeature.Commands.DeleteSeason;
using RiceProduction.Application.SeasonFeature.Queries.GetAllSeasons;

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
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSeason([FromBody] CreateSeasonCommand command)
        {
            var result = await _mediator.Send(command);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSeason(Guid id, [FromBody] UpdateSeasonCommand command)
        {
            if (id != command.SeasonId)
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
        public async Task<IActionResult> DeleteSeason(Guid id)
        {
            var command = new DeleteSeasonCommand { SeasonId = id };
            var result = await _mediator.Send(command);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}

