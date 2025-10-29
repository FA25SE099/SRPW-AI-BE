using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonalEconomicOverview;
using RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonCostAnalysis;
using RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetSeasonYieldAnalysis;
using RiceProduction.Application.ExpertSeasonalEconomicsFeature.Queries.GetHistoricalSeasonComparison;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/expert/seasonal-economics")]
    public class ExpertSeasonalEconomicsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ExpertSeasonalEconomicsController> _logger;

        public ExpertSeasonalEconomicsController(IMediator mediator, ILogger<ExpertSeasonalEconomicsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetSeasonalEconomicOverview(
            [FromQuery] Guid seasonId,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] Guid? groupId = null)
        {
            var query = new GetSeasonalEconomicOverviewQuery
            {
                SeasonId = seasonId,
                ClusterId = clusterId,
                GroupId = groupId
            };

            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("costs")]
        public async Task<IActionResult> GetSeasonCostAnalysis(
            [FromQuery] Guid seasonId,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] Guid? groupId = null,
            [FromQuery] Guid? riceVarietyId = null)
        {
            var query = new GetSeasonCostAnalysisQuery
            {
                SeasonId = seasonId,
                ClusterId = clusterId,
                GroupId = groupId,
                RiceVarietyId = riceVarietyId
            };

            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("yields")]
        public async Task<IActionResult> GetSeasonYieldAnalysis(
            [FromQuery] Guid seasonId,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] Guid? groupId = null)
        {
            var query = new GetSeasonYieldAnalysisQuery
            {
                SeasonId = seasonId,
                ClusterId = clusterId,
                GroupId = groupId
            };

            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("comparison")]
        public async Task<IActionResult> GetHistoricalSeasonComparison(
            [FromQuery] List<Guid>? seasonIds = null,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] int? year = null,
            [FromQuery] int? limit = 5)
        {
            var query = new GetHistoricalSeasonComparisonQuery
            {
                SeasonIds = seasonIds ?? new List<Guid>(),
                ClusterId = clusterId,
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
    }
}

