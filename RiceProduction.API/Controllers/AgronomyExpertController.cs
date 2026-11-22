using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
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
        var query = new GetAgronomyExpertsQuery
        {
            PageSize = request.PageSize,
            CurrentPage = request.CurrentPage,
            Search = request.Search,
            PhoneNumber = request.PhoneNumber,
            FreeOrAssigned = request.FreeOrAssigned
        };
        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgronomyExpert([FromBody] CreateAgronomyExpertCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToIActionResult(result);
    }

    [HttpGet("Get-by-id")]
    public async Task<IActionResult> GetAgronomyExpertById([FromQuery] Guid agronomyExpertId)
    {
        var result = await _mediator.Send(new GetAgronomyExpertByIdQuery { AgronomyExpertId = agronomyExpertId });
        return this.ToIActionResult(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "AgronomyExpert")]
    public async Task<IActionResult> GetCurrentAgronomyExpert()
    {
        var result = await _mediator.Send(new GetCurrentAgronomyExpertQuery());
        return this.ToIActionResult(result);
    }
}
