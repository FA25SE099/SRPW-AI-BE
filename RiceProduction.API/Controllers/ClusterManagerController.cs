using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerById;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterIdByManagerId;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterManagerRequests;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClusterManagerController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<ClusterManagerController> _logger;

    public ClusterManagerController(IMediator mediator, ILogger<ClusterManagerController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<ClusterManagerResponse>>>> GetClusterManagersPagingAndSearch([FromBody] ClusterManagerListRequest request)
    {
        var query = new GetClusterManagersQuery
        {
            PageSize = request.PageSize,
            CurrentPage = request.CurrentPage,
            Search = request.Search,
            PhoneNumber = request.PhoneNumber,
            FreeOrAssigned = request.FreeOrAssigned
        };
        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateClusterManager([FromBody] CreateClusterManagerCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToIActionResult(result);
    }

    [HttpGet("Get-by-id")]
    public async Task<IActionResult> GetClusterManagerById([FromQuery] Guid clusterManagerId)
    {
        var result = await _mediator.Send(new GetClusterManagerByIdQuery { ClusterManagerId = clusterManagerId });
        return this.ToIActionResult(result);
    }

    [HttpGet("get-cluster-id")]
    public async Task<IActionResult> GetClusterIdByManagerId([FromQuery] Guid clusterManagerId)
    {
        var result = await _mediator.Send(new GetClusterIdByManagerIdQuery { ClusterManagerId = clusterManagerId });
        return this.ToIActionResult(result);
    }
}
