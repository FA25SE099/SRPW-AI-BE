using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.RiceVarietyFeature.Commands;
using RiceProduction.Application.RiceVarietyFeature.Commands.CreateRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Commands.UpdateRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;
using RiceProduction.Application.RiceVarietyFeature.Queries.DownloadAllRiceVarietiesExcel;
using RiceProduction.Application.RiceVarietyFeature.Queries.DownloadRiceVarietySampleExcel;

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

        [HttpPost("download-excel")]
        public async Task<IActionResult> DownloadRiceVarietiesExcel([FromBody] DownloadAllRiceVarietiesExcelQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded || result.Data == null)
            {
                return BadRequest(new { message = result.Message });
            }
            return result.Data;
        }

        [HttpGet("download-sample-excel")]
        public async Task<IActionResult> DownloadSampleExcel()
        {
            var query = new DownloadRiceVarietySampleExcelQuery();
            var result = await _mediator.Send(query);
            if (!result.Succeeded || result.Data == null)
            {
                return BadRequest(new { message = result.Message });
            }
            return result.Data;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateRiceVariety([FromBody] CreateRiceVarietyCommand command)
        {
            var result = await _mediator.Send(command);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRiceVariety(Guid id, [FromBody] UpdateRiceVarietyCommand command)
        {
            if (id != command.RiceVarietyId)
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
        public async Task<IActionResult> DeleteRiceVariety(Guid id)
        {
            var command = new DeleteRiceVarietyCommand { RiceVarietyId = id };
            var result = await _mediator.Send(command);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}