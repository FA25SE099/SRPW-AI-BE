using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.WeatherProtocolRequests;
using RiceProduction.Application.Common.Models.Response.WeatherProtocolResponses;
using RiceProduction.Application.WeatherProtocolFeature.Commands.CreateWeatherProtocol;
using RiceProduction.Application.WeatherProtocolFeature.Commands.UpdateWeatherProtocol;
using RiceProduction.Application.WeatherProtocolFeature.Queries.GetAllWeatherProtocols;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherProtocolController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<WeatherProtocolController> _logger;

    public WeatherProtocolController(IMediator mediator, ILogger<WeatherProtocolController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<WeatherProtocolResponse>>>> GetAll([FromBody] WeatherProtocolListRequest request)
    {
        var query = new GetAllWeatherProtocolsQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            SearchName = request.SearchName,
            IsActive = request.IsActive
        };

        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateWeatherProtocolCommand command)
    {
        _logger.LogInformation(
            "Create WeatherProtocol request received: {ProtocolName}, Source: {Source}",
            command.Name,
            command.Source);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to create weather protocol: {Errors}",
                string.Join(", ", result.Errors ?? Array.Empty<string>()));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully created weather protocol with ID: {ProtocolId}", result.Data);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdateWeatherProtocolCommand command)
    {
        _logger.LogInformation("Update WeatherProtocol request received for ID: {ProtocolId}", command.Id);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to update weather protocol ID {ProtocolId}: {Errors}",
                command.Id, string.Join(", ", result.Errors ?? Array.Empty<string>()));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully updated weather protocol with ID: {ProtocolId}", result.Data);
        return Ok(result);
    }
}