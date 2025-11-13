using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager;
using RiceProduction.Application.ClusterManagerFeature.Queries.GetClusterManagerList;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterManagerRequests;
using RiceProduction.Application.Common.Models.Request.UavVendorRequests;
using RiceProduction.Application.Common.Models.Response.ClusterManagerResponses;
using RiceProduction.Application.Common.Models.Response.UavVendorResponses;
using RiceProduction.Application.UavVendorFeature.Commands.CreateUavVendor;
using RiceProduction.Application.UavVendorFeature.Commands.UpdateUavVendor;
using RiceProduction.Application.UavVendorFeature.Queries.GetUavVendorById;
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
    public async Task<IActionResult> CreateUavVendor([FromBody] UavVendorRequest request)
    {
        var command = new CreateUavVendorCommand
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            VendorName = request.VendorName,
            BusinessRegistrationNumber = request.BusinessRegistrationNumber,
            ServiceRatePerHa = request.ServiceRatePerHa,
            FleetSize = request.FleetSize,
            ServiceRadius = request.ServiceRadius,
            EquipmentSpecs = request.EquipmentSpecs,
            OperatingSchedule = request.OperatingSchedule
        };
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUavVendor([FromBody] UpdateUavVendorCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("get-by-id")]
    public async Task<ActionResult<PagedResult<List<UavVendorResponse>>>> GetUavVendorById([FromForm] GetUavVendorByIdQuery query)
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
