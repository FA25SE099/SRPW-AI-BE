using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.UAVFeature.Commands.ReportServiceOrder;
using RiceProduction.Application.UAVFeature.Queries.GeUAVOrderDetail;
using RiceProduction.Application.UAVFeature.Queries.GetClusterServiceOrdersByManager;
using RiceProduction.Application.UAVFeature.Queries.GetVendorServiceOrders;
using System.Collections.Generic;
using RiceProduction.Application.UAVFeature.Commands.CreateUavOrder;
using RiceProduction.Application.UAVFeature.Queries.GetPlotsReadyForUav;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/uav/orders")]
public class UavController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUser _currentUser;

    public UavController(IMediator mediator, IUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetVendorServiceOrders([FromQuery] GetVendorServiceOrdersQuery query)
    {
        var vendorId = _currentUser.Id;
        if (vendorId == null)
        {
            return Unauthorized(PagedResult<List<UavServiceOrderResponse>>.Failure("Vendor is not authenticated.", "Unauthorized"));
        }

        query.VendorId = vendorId.Value;
        
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("cluster-managers")]
    public async Task<IActionResult> GetClusterServiceOrdersByClusterManagerId([FromQuery] GetClusterServiceOrdersByManagerQuery query)
    {
        var currentUserId = _currentUser.Id;
        if (currentUserId == null)
        {
            return Unauthorized(PagedResult<List<UavServiceOrderResponse>>.Failure("User is not authenticated.", "Unauthorized"));
        }

        query.ClusterManagerId = currentUserId.Value;

        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetUavOrderDetail(Guid orderId)
    {
        //var vendorId = _currentUser.Id;
        //if (vendorId == null)
        //{
        //    return Unauthorized(Result<UavOrderDetailResponse>.Failure("Vendor is not authenticated.", "Unauthorized"));
        //}

        var query = new GetUavOrderDetailQuery 
        { 
            OrderId = orderId, 
            //VendorId = vendorId.Value 
        };
        
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    
    [HttpPost("{orderId}/plots/{plotId}/report")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ReportServiceOrderCompletion(
        Guid orderId, 
        Guid plotId, 
        [FromForm] ReportServiceOrderCompletionCommand command)
    {
        var vendorId = _currentUser.Id;
        if (vendorId == null)
        {
            return Unauthorized(Result<Guid>.Failure("Vendor is not authenticated.", "Unauthorized"));
        }

        if (orderId != command.OrderId || plotId != command.PlotId)
        {
            return BadRequest("ID mismatch between URL and body/command fields.");
        }

        command.VendorId = vendorId.Value;
        
        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpPost("uav/orders")]
    public async Task<IActionResult> CreateUavOrder([FromBody] CreateUavOrderCommand command)
    {
        var managerId = _currentUser.Id;
        if (managerId == null)
        {
            return Unauthorized(Result<Guid>.Failure("Cluster Manager authentication required.", "Unauthorized"));
        }
        
        command.ClusterManagerId = managerId.Value;

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet("uav/ready-plots")]
    public async Task<IActionResult> GetPlotsReadyForUav([FromQuery] GetPlotsReadyForUavQuery query)
    {   
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}