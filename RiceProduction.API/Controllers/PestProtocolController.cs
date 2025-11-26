using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PestProtocolRequests;
using RiceProduction.Application.Common.Models.Response.PestProtocolResponses;
using RiceProduction.Application.PestProtocolFeature.Commands.CreatePestProtocol;
using RiceProduction.Application.PestProtocolFeature.Commands.UpdatePestProtocol;
using RiceProduction.Application.PestProtocolFeature.Queries.GetAllPestProtocols;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PestProtocolController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<PestProtocolController> _logger;

    public PestProtocolController(IMediator mediator, ILogger<PestProtocolController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<PestProtocolResponse>>>> GetAll([FromBody] PestProtocolListRequest request)
    {
        var query = new GetAllPestProtocolsQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            SearchName = request.SearchName,
            IsActive = request.IsActive
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreatePestProtocolCommand command)
    {
        try
        {
            _logger.LogInformation(
                "Create PestProtocol request received: {ProtocolName}, Type: {Type}",
                command.Name,
                command.Type);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to create pest protocol: {Errors}",
                    string.Join(", ", result.Errors ?? new string[0]));
                return BadRequest(result);
            }

            _logger.LogInformation("Successfully created pest protocol with ID: {ProtocolId}", result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while creating pest protocol");
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }

    [HttpPut]
    public async Task<ActionResult<Result<Guid>>> Update([FromBody] UpdatePestProtocolCommand command)
    {
        try
        {

            _logger.LogInformation("Update PestProtocol request received for ID: {ProtocolId}", command.Id);

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to update pest protocol ID {ProtocolId}: {Errors}",
                    command.Id, string.Join(", ", result.Errors ?? new string[0]));

                return BadRequest(result);
            }

            _logger.LogInformation("Successfully updated pest protocol with ID: {ProtocolId}", result.Data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while updating pest protocol ID: {ProtocolId}", command.Id);
            return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
        }
    }
}
