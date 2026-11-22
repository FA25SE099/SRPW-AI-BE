using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.RiceVarietyFeature.Commands;
using RiceProduction.Application.RiceVarietyFeature.Commands.CreateRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Commands.UpdateRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Commands.DeleteRiceVariety;
using RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;
using RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarietyCategories;
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
            return this.ToIActionResult(result);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetAllCategories([FromQuery] bool? isActive)
        {
            var result = await _mediator.Send(new GetAllRiceVarietyCategoriesQuery { IsActive = isActive });
            return this.ToIActionResult(result);
        }

        [HttpPost]
        [Route("change-rice-season")]
        public async Task<IActionResult> ChangeRiceSeason([FromBody] ChangeRiceSeasonCommand query)
        {
            var result = await _mediator.Send(query);
            return this.ToIActionResult(result);
        }

        [HttpPost("download-excel")]
        public async Task<IActionResult> DownloadRiceVarietiesExcel([FromBody] DownloadAllRiceVarietiesExcelQuery query)
        {
            var result = await _mediator.Send(query);
            return this.ToFileActionResult(result);
        }

        [HttpGet("download-sample-excel")]
        public async Task<IActionResult> DownloadSampleExcel()
        {
            var result = await _mediator.Send(new DownloadRiceVarietySampleExcelQuery());
            return this.ToFileActionResult(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateRiceVariety([FromBody] CreateRiceVarietyCommand command)
        {
            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRiceVariety(Guid id, [FromBody] UpdateRiceVarietyCommand command)
        {
            if (id != command.RiceVarietyId)
            {
                return BadRequest(new { message = "Route ID does not match command ID" });
            }

            var result = await _mediator.Send(command);
            return this.ToIActionResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRiceVariety(Guid id)
        {
            var result = await _mediator.Send(new DeleteRiceVarietyCommand { RiceVarietyId = id });
            return this.ToIActionResult(result);
        }
    }
}