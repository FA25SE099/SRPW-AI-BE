using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaterialController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("AllMaterialPaging")]
    public async Task<ActionResult<PagedResult<List<MaterialResponse>>>> AllMaterialPaging([FromBody] MaterialListRequest request)
    {
        var query = new GetAllMaterialByTypeQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            MaterialType = request.Type
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
