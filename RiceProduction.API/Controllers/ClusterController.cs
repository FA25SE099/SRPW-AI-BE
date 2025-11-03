using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClusterController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<PlotController> _logger;

    public ClusterController(IMediator mediator, ILogger<PlotController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    // to done it: create cluster, get cluster manager list, create cluster manager, get cluster list, send sms, change password
    [HttpPost]
    public async Task<IActionResult> CreateCluster(CreateClusterCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
