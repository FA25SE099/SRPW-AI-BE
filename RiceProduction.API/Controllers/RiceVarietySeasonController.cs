using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.RiceVarietySeasonFeature.Commands.CreateRiceVarietySeason;
using RiceProduction.Application.RiceVarietySeasonFeature.Commands.UpdateRiceVarietySeason;
using RiceProduction.Application.RiceVarietySeasonFeature.Commands.DeleteRiceVarietySeason;
using RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetAllRiceVarietySeasons;
using RiceProduction.Application.RiceVarietySeasonFeature.Queries.GetRiceVarietySeasonDetail;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RiceVarietySeasonController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<RiceVarietySeasonController> _logger;

        public RiceVarietySeasonController(IMediator mediator, ILogger<RiceVarietySeasonController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] GetAllRiceVarietySeasonsQuery query)
        {
            var result = await _mediator.Send(query);
            return this.ToIActionResult(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetDetail(Guid id)
        {
            var query = new GetRiceVarietySeasonDetailQuery { RiceVarietySeasonId = id };
            var result = await _mediator.Send(query);
            return this.ToIActionResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRiceVarietySeasonCommand command)
        {
            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRiceVarietySeasonCommand command)
        {
            if (id != command.RiceVarietySeasonId)
            {
                return BadRequest(new { message = "Route ID does not match command ID" });
            }

            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var command = new DeleteRiceVarietySeasonCommand { RiceVarietySeasonId = id };
            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }
    }
}

