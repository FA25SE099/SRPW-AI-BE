using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.ClusterFeature.Commands.CreateCluster;
using RiceProduction.Application.ClusterFeature.Commands.UpdateCluster;
using RiceProduction.Application.ClusterFeature.Queries.GetAllClustersPaging;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterAvailableSeasons;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterCurrentSeason;
using RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.ClusterRequests;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Application.Common.Models.Response.ClusterResponses;

namespace RiceProduction.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClusterController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<ClusterController> _logger;

    public ClusterController(IMediator mediator, ILogger<ClusterController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCluster(CreateClusterCommand command)
    {
        return this.ToIActionResult(await _mediator.Send(command));
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
            return this.ToActionResult(await _mediator.Send(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting clusters");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPut("Update-name-and-human-resource")]
    public async Task<ActionResult<Result<Guid>>> EditClusterNameAndHumanResource([FromBody] UpdateClusterCommand input)
    {
        return this.ToActionResult(await _mediator.Send(input));
    }

    [HttpGet("{clusterId}/history")]
    [Obsolete("This endpoint is deprecated. Use GET /api/YearSeason/cluster/{clusterId} instead to get past YearSeasons.", false)]
    public async Task<ActionResult<Result<ClusterHistoryResponse>>> GetClusterHistory(
        Guid clusterId,
        [FromQuery] Guid? seasonId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? limit = 5)
    {
        try
        {
            AddDeprecationHeaders("Use GET /api/YearSeason/cluster/{clusterId} instead");

            var query = new GetClusterHistoryQuery
            {
                ClusterId = clusterId,
                SeasonId = seasonId,
                Year = year,
                Limit = limit
            };

            return this.ToActionResult(await _mediator.Send(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting cluster history for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{clusterId}/current-season")]
    [Obsolete("This endpoint is deprecated. Use GET /api/YearSeason/cluster/{clusterId} to get current season, then GET /api/YearSeason/{id}/dashboard for details.", false)]
    public async Task<ActionResult<Result<ClusterCurrentSeasonResponse>>> GetClusterCurrentSeason(Guid clusterId)
    {
        try
        {
            AddDeprecationHeaders("Use GET /api/YearSeason/cluster/{clusterId} and /api/YearSeason/{id}/dashboard instead");

            var query = new GetClusterCurrentSeasonQuery
            {
                ClusterId = clusterId
            };

            return this.ToActionResult(await _mediator.Send(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting current season for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{clusterId}/seasons")]
    [Obsolete("This endpoint is deprecated. Use GET /api/YearSeason/cluster/{clusterId} instead to get all YearSeasons categorized as current/past/upcoming.", false)]
    public async Task<ActionResult<Result<ClusterSeasonsResponse>>> GetClusterAvailableSeasons(
        Guid clusterId,
        [FromQuery] bool includeEmpty = true,
        [FromQuery] int? limit = null)
    {
        try
        {
            AddDeprecationHeaders("Use GET /api/YearSeason/cluster/{clusterId} instead");

            var query = new GetClusterAvailableSeasonsQuery
            {
                ClusterId = clusterId,
                IncludeEmpty = includeEmpty,
                Limit = limit
            };

            return this.ToActionResult(await _mediator.Send(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting available seasons for cluster {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    private void AddDeprecationHeaders(string message)
    {
        Response.Headers.Add("X-Deprecated", "true");
        Response.Headers.Add("X-Deprecated-Message", message);
        Response.Headers.Add("X-New-Endpoint", "/api/YearSeason/cluster/{clusterId}");
    }
}