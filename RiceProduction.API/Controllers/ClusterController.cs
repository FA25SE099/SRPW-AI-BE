using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.ClusterFeature.Commands.UpdateCluster;
using RiceProduction.Application.ClusterFeature.Queries.GetAllClustersPaging;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterCurrentSeason;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterAvailableSeasons;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterRequests;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.Common.Models.Response.ClusterResponses;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.PlotFeature.Commands.EditPlot;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClusterController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<PlotController> _logger;

    public ClusterController(IMediator mediator, ILogger<PlotController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    // to done it: create cluster, get cluster manager list, create cluster manager, get cluster list, send sms, change password
    [HttpPost]
    public async Task<IActionResult> CreateCluster(CreateClusterCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpPost("Get-all")]
    public async Task<ActionResult<PagedResult<List<ClusterResponse>>>> GetAllClusters([FromBody] ClusterListRequest request)
    {
        try
        {
            var query = new GetAllClustersQuery
            {
                CurrentPage = request.CurrentPage,
                PageSize = request.PageSize,
                ClusterNameSearch = request.ClusterNameSearch,
                ManagerExpertNameSearch = request.ManagerExpertNameSearch,
                PhoneNumber = request.PhoneNumber,
                SortBy = request.SortBy
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
            _logger.LogError(ex, "Error occurred while getting clusters");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPut("Update-name-and-human-resource")]
    public async Task<ActionResult<Result<UpdateClusterCommand>>> EditClusterNameAndHumanResource([FromBody] UpdateClusterCommand input)
    {
        var command = input;
        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("{clusterId}/history")]
    public async Task<ActionResult<Result<ClusterHistoryResponse>>> GetClusterHistory(
        Guid clusterId,
        [FromQuery] Guid? seasonId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? limit = 5)
    {
        try
        {
            var query = new GetClusterHistoryQuery
            {
                ClusterId = clusterId,
                SeasonId = seasonId,
                Year = year,
                Limit = limit
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
            _logger.LogError(ex, "Error occurred while getting cluster history for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{clusterId}/current-season")]
    public async Task<ActionResult<Result<ClusterCurrentSeasonResponse>>> GetClusterCurrentSeason(Guid clusterId)
    {
        try
        {
            var query = new GetClusterCurrentSeasonQuery
            {
                ClusterId = clusterId
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
            _logger.LogError(ex, "Error occurred while getting current season for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{clusterId}/seasons")]
    public async Task<ActionResult<Result<ClusterSeasonsResponse>>> GetClusterAvailableSeasons(
        Guid clusterId,
        [FromQuery] bool includeEmpty = true,
        [FromQuery] int? limit = null)
    {
        try
        {
            var query = new GetClusterAvailableSeasonsQuery
            {
                ClusterId = clusterId,
                IncludeEmpty = includeEmpty,
                Limit = limit
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
            _logger.LogError(ex, "Error occurred while getting available seasons for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}