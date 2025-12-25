using Google.Protobuf.WellKnownTypes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmLogFeature.Commands.CreateFarmLog;
using RiceProduction.Application.FarmLogFeature.Queries;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationPlot;
using RiceProduction.Application.FarmLogFeature.Queries.GetByProductionPlanTask;
using RiceProduction.Application.FarmLogFeature.Queries.GetByCultivationTask;

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
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    
    [HttpPost("farm-logs/by-cultivation")]
    [ProducesResponseType(typeof(PagedResult<List<FarmLogDetailResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFarmLogsByCultivation([FromBody] GetFarmLogsByCultivationRequest request)
    {
        var query = new GetFarmLogsByCultivationQuery
        {
            PlotCultivationId = request.PlotCultivationId,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("farm-logs/by-production-plan-task")]
    [ProducesResponseType(typeof(PagedResult<List<FarmLogDetailResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFarmLogsByProductionPlanTask([FromBody] GetFarmLogsByProductionPlanTaskRequest request)
    {
        var query = new GetFarmLogsByProductionPlanTaskQuery
        {
            ProductionPlanTaskId = request.ProductionPlanTaskId,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("farm-logs/by-cultivation-task")]
    [ProducesResponseType(typeof(PagedResult<List<FarmLogDetailResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFarmLogsByCultivationTask([FromBody] GetFarmLogsByCultivationTaskRequest request)
    {
        var query = new GetFarmLogsByCultivationTaskQuery
        {
            CultivationTaskId = request.CultivationTaskId,
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded)
        {
            if (result.Message?.Contains("not found") == true)
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public class GetFarmLogsByCultivationRequest
{
    public Guid PlotCultivationId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class GetFarmLogsByProductionPlanTaskRequest
{
    public Guid ProductionPlanTaskId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetFarmLogsByCultivationTaskRequest
{
    public Guid CultivationTaskId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

