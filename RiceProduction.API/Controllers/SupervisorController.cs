using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Request.SupervisorRequests;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;
using RiceProduction.Application.SupervisorFeature.Queries;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController : Controller
{
    private readonly IMediator _mediator;

    public SupervisorController(IMediator mediator)
    {
        _mediator = mediator;
    }


    [HttpPost("get-paging")]
    public async Task<ActionResult<PagedResult<List<SupervisorResponse>>>> GetAllSupervisorPaging([FromForm] SupervisorListRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userIdReal))
        {
            return Unauthorized(PagedResult<List<SupervisorResponse>>.Failure("User not authenticated"));
        }
        var query = new GetAllSupervisorQuery
        {
            ClusterId = userIdReal,
            SearchNameOrEmail = request.SearchNameOrEmail,
            SearchPhoneNumber = request.SearchPhoneNumber,
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
}
