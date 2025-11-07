using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.GroupRequests;
using RiceProduction.Application.Common.Models.Response.GroupResponses;
using RiceProduction.Application.GroupFeature.Queries.GetAllGroup;
using RiceProduction.Application.GroupFeature.Queries.GetGroupDetail;
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
<<<<<<< HEAD
    // [HttpPost()]
    // public async Task<ActionResult<PagedResult<List<GroupResponse>>>> GetGroupsByClusterIdPaging([FromBody] GroupListRequest request)
    // {

    //     var query = new GetGroupsByClusterManagerIdQuery()
    //     {
    //         ClusterManagerUserId = new Guid("019a0806-24ef-7df0-ac28-74495da52a12"),
    //         CurrentPage = request.CurrentPage,
    //         PageSize = request.PageSize
    //     };
    //     var result = await _mediator.Send(query);
    //     if (!result.Succeeded)
    //     {
    //         return BadRequest(result);
    //     }
    //     return Ok(result);
    // }
=======
    [HttpPost()]
    public async Task<ActionResult<PagedResult<List<GroupResponse>>>> GetGroupsByClusterIdPaging([FromBody] GroupListRequest request)
    {
        
        var query = new GetGroupsByClusterManagerIdQuery
        {
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
>>>>>>> 255dffccd55df376729e3514ac75fe99918f0c84
    [HttpGet("{id}")]
    public async Task<IActionResult> GetGroupDetail(Guid id)
    {
        var result = await _mediator.Send(new GetGroupDetailQuery { GroupId = id });

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> GetAllGroups()    
    {
        var query = new GetAllGroupQuery();
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);

    }

}
