using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/farmer/cultivation-tasks")]
    public class FarmerCultivationTaskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FarmerCultivationTaskController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetFarmerCultivationTasks(
            [FromQuery] Guid farmerId,
            [FromQuery] Guid? seasonId = null,
            [FromQuery] Guid? plotId = null,
            [FromQuery] TaskStatus? status = null,
            [FromQuery] bool? includePastSeasons = false,
            [FromQuery] bool? includeCompleted = true)
        {
            var query = new GetFarmerCultivationTasksQuery
            {
                FarmerId = farmerId,
                SeasonId = seasonId,
                PlotId = plotId,
                Status = status,
                IncludePastSeasons = includePastSeasons,
                IncludeCompleted = includeCompleted
            };

            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}

