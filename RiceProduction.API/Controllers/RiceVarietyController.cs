using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.RiceVarietyFeature.Commands;
using RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RiceVarietyController : ControllerBase
    {
        private readonly IMediator _mediator;

        public RiceVarietyController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] GetAllRiceVarietiesQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpPost]
        [Route("change-rice-season")]

        public async Task<IActionResult> ChangeRiceSeason([FromBody] ChangeRiceSeasonCommand query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}