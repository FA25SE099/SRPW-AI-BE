using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetCultivationTaskDetail;
using RiceProduction.Application.CultivationPlanFeature.Queries.GetTodayTask;
using RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/farmer/cultivation-tasks")]
    public class FarmerCultivationTaskController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUser  _currentUser;

        public FarmerCultivationTaskController(IMediator mediator, IUser currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
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
        [HttpGet("outstanding-tasks")]
        public async Task<IActionResult> GetOutstandingTasks([FromQuery] GetTodayTasksQuery query)
        {
            var farmerId = _currentUser.Id;
            if (farmerId == null)
            {
                return Unauthorized(Result<List<TodayTaskResponse>>.Failure("User is not authenticated.", "Unauthorized"));
            }

            query.FarmerId = farmerId.Value;

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpGet("{cultivationTaskId}")]
        public async Task<IActionResult> GetCultivationTaskDetail([FromRoute] Guid cultivationTaskId)
        {
            var query = new GetCultivationTaskDetailQuery
            {
                CultivationTaskId = cultivationTaskId
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

