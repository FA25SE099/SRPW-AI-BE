using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.AgronomyExpertFeature.Commands.CreateAgronomyExpert;

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
}
