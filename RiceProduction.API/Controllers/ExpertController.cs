using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ProductionPlanFeature.Commands.ApproveRejectPlan;
using RiceProduction.Application.ProductionPlanFeature.Commands.EditPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetApprovedPlan;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPendingApprovals;
using RiceProduction.Application.ProductionPlanFeature.Queries.GetPlanDetail;
namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/expert")]
    public class ExpertController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ExpertController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("pending-approvals")]
        public async Task<IActionResult> GetPendingApprovals([FromQuery] GetPendingApprovalsQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedPlans([FromQuery] GetApprovedPlansQuery query)
        {
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("plans/{planId}")]
        public async Task<IActionResult> GetPlanDetails(Guid planId)
        {
            var query = new GetPlanDetailsForExpertQuery { PlanId = planId };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpPut("{planId}")]
        public async Task<IActionResult> EditPlan(Guid planId, [FromBody] EditPlanCommand command)
        {
            if (planId != command.PlanId)
            {
                return BadRequest("Plan ID in URL does not match Plan ID in body.");
            }

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            
            return NoContent(); 
        }

        [HttpPost("{planId}/approve")]
        public async Task<IActionResult> ApproveRejectPlan(Guid planId, [FromBody] ApproveRejectPlanCommand command)
        {
            if (planId != command.PlanId)
            {
                return BadRequest("Plan ID in URL does not match Plan ID in body.");
            }

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}