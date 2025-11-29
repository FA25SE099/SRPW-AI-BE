using MediatR;
using Microsoft.AspNetCore.Mvc;
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
        try
        {
            _logger.LogInformation("GetAll emergency protocols request received");

            //var query = new GetAllEmergencyProtocolsQuery
            //{
            //    CurrentPage = currentPage,
            //    PageSize = pageSize,
            //    CategoryId = categoryId,
            //    SearchTerm = searchTerm,
            //    IsActive = isActive
            //};

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve emergency protocols: {Errors}",
                    string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting emergency protocols");
            return StatusCode(500, PagedResult<List<EmergencyProtocolDto>>.Failure("An unexpected error occurred"));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<EmergencyProtocolDetailDto>>> GetDetail(Guid id)
    {
        try
        {
            _logger.LogInformation("GetEmergencyProtocolDetail request received for ID: {ProtocolId}", id);

            var query = new GetEmergencyProtocolDetailQuery { EmergencyProtocolId = id };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve emergency protocol detail for ID {ProtocolId}: {Errors}",
                    id, string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while getting emergency protocol detail for ID: {ProtocolId}", id);
            return StatusCode(500, Result<EmergencyProtocolDetailDto>.Failure("An unexpected error occurred"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateEmergencyProtocolCommand command)
    {
        try
        {
            _logger.LogInformation(
                "Create EmergencyProtocol request received: {ProtocolName}, Thresholds: {ThresholdCount}",
                command.PlanName,
                command.Thresholds?.Count ?? 0);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to create emergency protocol: {Errors}",
                    string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully created emergency protocol with ID: {ProtocolId}", result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while creating emergency protocol");
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdateEmergencyProtocolCommand command)
    {
        try
        {

            _logger.LogInformation("UpdateEmergencyProtocol request received for ID: {ProtocolId}", command.EmergencyProtocolId);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to update emergency protocol ID {ProtocolId}: {Errors}",
                    command.EmergencyProtocolId, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation("Successfully updated emergency protocol ID: {ProtocolId}", command.EmergencyProtocolId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error occurred while updating emergency protocol ID: {ProtocolId}",
                command.EmergencyProtocolId);
            return StatusCode(500, Result<Guid>.Failure(
                "An unexpected error occurred"));
        }
    }
}