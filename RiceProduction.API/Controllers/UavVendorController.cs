using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterManagerRequests;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using RiceProduction.Application.UavVendorFeature.Commands.CreateUavVendor;
using RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorList;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UavVendorController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<UavVendorController> _logger;

    public UavVendorController(IMediator mediator, ILogger<UavVendorController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUavVendor([FromBody] CreateUavVendorCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<UavVendorResponse>>>> GetUavVendorsPagingAndSearch([FromBody] GetUavVendorQuery query)
    {
        try
        {
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

}
