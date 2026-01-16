using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.AgronomyExpertFeature.Commands.CreateAgronomyExpert;
using RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertById;
using RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertList;
using RiceProduction.Application.AgronomyExpertFeature.Queries.GetCurrentAgronomyExpert;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.AgronomyExpertRequests;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AgronomyExpertController : Controller
{
    private readonly ILogger<AgronomyExpertController> _logger;
    private readonly IMediator _mediator;

    public AgronomyExpertController(ILogger<AgronomyExpertController> logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<AgronomyExpertResponse>>>> GetAgronomyExpertsPagingAndSearch([FromBody] AgronomyExpertListRequest request)
    {
        try
        {
            var query = new GetAgronomyExpertsQuery()
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
            _logger.LogError(ex, "Error occurred while getting agronomy experts");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgronomyExpert([FromBody] CreateAgronomyExpertCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpGet("Get-by-id")]
    public async Task<IActionResult> GetAgronomyExpertById([FromQuery] Guid agronomyExpertId)
    {
        try
        {
            var query = new GetAgronomyExpertByIdQuery()
            {
                AgronomyExpertId = agronomyExpertId
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
            _logger.LogError(ex, "Error occurred while getting agronomy expert by id");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("me")]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> GetCurrentAgronomyExpert()
    {
        try
        {
            var query = new GetCurrentAgronomyExpertQuery();
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting current agronomy expert");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
