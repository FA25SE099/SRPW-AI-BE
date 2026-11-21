using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.EmergencyProtocolResponses;
using RiceProduction.Application.EmergencyProtocolFeature.Commands.CreateEmergencyProtocol;
using RiceProduction.Application.EmergencyProtocolFeature.Commands.UpdateEmergencyProtocol;
using RiceProduction.Application.EmergencyProtocolFeature.Queries.GetAllEmergencyProtocols;
using RiceProduction.Application.EmergencyProtocolFeature.Queries.GetEmergencyProtocolDetail;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmergencyProtocolController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmergencyProtocolController> _logger;

    public EmergencyProtocolController(IMediator mediator, ILogger<EmergencyProtocolController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<EmergencyProtocolDto>>>> GetAll([FromBody] GetAllEmergencyProtocolsQuery query)
    {
        _logger.LogInformation("GetAll emergency protocols request received");

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve emergency protocols: {Errors}", FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<EmergencyProtocolDetailDto>>> GetDetail(Guid id)
    {
        _logger.LogInformation("GetEmergencyProtocolDetail request received for ID: {ProtocolId}", id);

        var query = new GetEmergencyProtocolDetailQuery { EmergencyProtocolId = id };
        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to retrieve emergency protocol detail for ID {ProtocolId}: {Errors}",
                id, FormatErrors(result.Errors));
        }

        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateEmergencyProtocolCommand command)
    {
        _logger.LogInformation(
            "Create EmergencyProtocol request received: {ProtocolName}, Thresholds: {ThresholdCount}",
            command.PlanName,
            command.Thresholds?.Count ?? 0);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to create emergency protocol: {Errors}", FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully created emergency protocol with ID: {ProtocolId}", result.Data);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdateEmergencyProtocolCommand command)
    {
        _logger.LogInformation("UpdateEmergencyProtocol request received for ID: {ProtocolId}", command.EmergencyProtocolId);

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Failed to update emergency protocol ID {ProtocolId}: {Errors}",
                command.EmergencyProtocolId, FormatErrors(result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Successfully updated emergency protocol ID: {ProtocolId}", command.EmergencyProtocolId);
        return Ok(result);
    }

    private static string FormatErrors(IEnumerable<string>? errors) =>
        string.Join(", ", errors ?? Array.Empty<string>());
}