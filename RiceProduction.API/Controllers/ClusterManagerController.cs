using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerById;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterIdByManagerId;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterManagerRequests;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
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
        try
        {
            var query = new GetClusterManagersQuery()
            {
                PageSize = request.PageSize,
                CurrentPage = request.CurrentPage,
                Search = request.Search,
                PhoneNumber = request.PhoneNumber,
                FreeOrAssigned = request.FreeOrAssigned
            };
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting free cluster managers");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateClusterManager([FromBody] CreateClusterManagerCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpGet("Get-by-id")]
    public async Task<IActionResult> GetClusterManagerById([FromQuery] Guid clusterManagerId)
    {
        try
        {
            var query = new GetClusterManagerByIdQuery
            {
                ClusterManagerId = clusterManagerId
            };
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting cluster manager by ID");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("get-cluster-id")]
    public async Task<IActionResult> GetClusterIdByManagerId([FromQuery] Guid clusterManagerId)
    {
        try
        {
            var query = new GetClusterIdByManagerIdQuery
            {
                ClusterManagerId = clusterManagerId
            };
            var result = await _mediator.Send(query);
            
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting cluster ID by manager ID");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
