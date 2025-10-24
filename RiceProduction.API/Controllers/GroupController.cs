using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.GroupFeature.Queries.GetGroupDetail;
using RiceProduction.Application.Common.Models;
using MediatR;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/group")]
    public class GroupController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GroupController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroupDetail(Guid id)
        {
            var query = new GetGroupDetailQuery { GroupId = id };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
    
}
