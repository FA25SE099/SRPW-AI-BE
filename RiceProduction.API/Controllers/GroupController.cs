using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.GroupRequests;
using RiceProduction.Application.Common.Models.Response.GroupResponses;
using RiceProduction.Application.GroupFeature.Queries.GetGroupsByClusterId;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GroupController : Controller
{
    private readonly IMediator _mediator;

    public GroupController(IMediator mediator)
    {
        _mediator = mediator;
    }
    [HttpPost()]
    public async Task<ActionResult<PagedResult<List<GroupResponse>>>> GetGroupsByClusterIdPaging([FromBody] GroupListRequest request)
    {
        
        var query = new GetGroupsByClusterManagerIdQuery()
        {
            ClusterManagerUserId = new Guid("019a0806-24ef-7df0-ac28-74495da52a12"),
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
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
