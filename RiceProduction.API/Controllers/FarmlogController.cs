using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;
namespace RiceProduction.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class FarmlogController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUser _currentUser;

    public FarmlogController(IMediator mediator, IUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }
    [HttpPost("farm-logs")]
    [Consumes("multipart/form-data")] // Quan trọng để Swagger nhận diện upload file
    public async Task<IActionResult> CreateFarmLog([FromForm] CreateFarmLogCommand command)
    {
        var farmerId = _currentUser.Id;
        if (farmerId == null)
        {
            return Unauthorized(Result<Guid>.Failure("User is not authenticated.", "Unauthorized"));
        }

        command.FarmerId = farmerId.Value;

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet("farm-logs/by-cultivation")]
    public async Task<IActionResult> GetFarmLogsByCultivation([FromQuery] GetFarmLogsByCultivationQuery query)
    {
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}