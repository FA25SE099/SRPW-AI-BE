using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;
using RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;
using RiceProduction.Application.SeasonFeature.Queries.GetAvailableRiceVarietiesForSeason;

namespace RiceProduction.API.Controllers;

/// <summary>
/// Controller for farmer cultivation selection functionality
/// </summary>
[ApiController]
[Route("api/farmer/cultivation")]
public class FarmerCultivationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FarmerCultivationController> _logger;
    private readonly IUser _currentUser;
    private readonly IApplicationDbContext _context;

    public FarmerCultivationController(
        IMediator mediator,
        ILogger<FarmerCultivationController> logger,
        IUser currentUser,
        IApplicationDbContext context)
    {
        _mediator = mediator;
        _logger = logger;
        _currentUser = currentUser;
        _context = context;
    }

    /// <summary>
    /// Get available rice varieties for a specific season
    /// </summary>
    /// <param name="seasonId">Season ID</param>
    /// <param name="onlyRecommended">Filter to show only recommended varieties (default: true)</param>
    /// <returns>List of available rice varieties</returns>
    [HttpGet("season/{seasonId}/available-varieties")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableVarieties(
        Guid seasonId,
        [FromQuery] bool onlyRecommended = true)
    {
        var query = new GetAvailableRiceVarietiesForSeasonQuery
        {
            SeasonId = seasonId,
            OnlyRecommended = onlyRecommended
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Get available rice varieties for a specific year season (extracts seasonId automatically)
    /// </summary>
    /// <param name="yearSeasonId">Year Season ID</param>
    /// <param name="onlyRecommended">Filter to show only recommended varieties (default: true)</param>
    /// <returns>List of available rice varieties</returns>
    [HttpGet("year-season/{yearSeasonId}/available-varieties")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableVarietiesForYearSeason(
        Guid yearSeasonId,
        [FromQuery] bool onlyRecommended = true)
    {
        var yearSeason = await _context.YearSeasons
            .AsNoTracking()
            .Where(ys => ys.Id == yearSeasonId)
            .Select(ys => new { ys.SeasonId })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (yearSeason == null)
        {
            return NotFound(new { message = "Year Season not found" });
        }

        var query = new GetAvailableRiceVarietiesForSeasonQuery
        {
            SeasonId = yearSeason.SeasonId,
            OnlyRecommended = onlyRecommended
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Validate cultivation preferences before confirmation
    /// </summary>
    /// <param name="query">Validation query parameters</param>
    /// <returns>Validation result with errors, warnings, and recommendations</returns>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidatePreferences(
        [FromBody] ValidateCultivationPreferencesQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Select and confirm cultivation preferences for a plot
    /// </summary>
    /// <param name="command">Selection command parameters</param>
    /// <returns>Confirmed cultivation preference details</returns>
    [HttpPost("select")]
    [Authorize(Roles = "Farmer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SelectPreferences(
        [FromBody] SelectCultivationPreferencesCommand command)
    {
        // Additional authorization check: ensure farmer owns the plot
        if (_currentUser.Id.HasValue)
        {
            var plot = await _context.Plots
                .AsNoTracking()
                .Where(p => p.Id == command.PlotId)
                .Select(p => new { p.FarmerId })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (plot == null)
            {
                return NotFound(new { message = "Plot not found" });
            }

            if (plot.FarmerId != _currentUser.Id.Value)
            {
                _logger.LogWarning(
                    "Farmer {FarmerId} attempted to select cultivation for plot {PlotId} owned by {OwnerId}",
                    _currentUser.Id.Value, command.PlotId, plot.FarmerId);
                return Forbid();
            }
        }

        return this.ToIActionResult(await _mediator.Send(command));
    }

    /// <summary>
    /// Get all cultivation selections for a farmer in a specific year season
    /// </summary>
    /// <param name="yearSeasonId">Year Season ID</param>
    /// <returns>Farmer's cultivation selections</returns>
    [HttpGet("my-selections/{yearSeasonId}")]
    [Authorize(Roles = "Farmer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMySelections(Guid yearSeasonId)
    {
        if (!_currentUser.Id.HasValue)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        var query = new GetFarmerCultivationSelectionsQuery
        {
            FarmerId = _currentUser.Id.Value,
            YearSeasonId = yearSeasonId
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Get cultivation selections for a specific farmer (Admin/Expert access)
    /// </summary>
    /// <param name="farmerId">Farmer ID</param>
    /// <param name="yearSeasonId">Year Season ID</param>
    /// <returns>Farmer's cultivation selections</returns>
    [HttpGet("farmer/{farmerId}/selections/{yearSeasonId}")]
    [Authorize(Roles = "Admin,AgronomyExpert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFarmerSelections(Guid farmerId, Guid yearSeasonId)
    {
        var query = new GetFarmerCultivationSelectionsQuery
        {
            FarmerId = farmerId,
            YearSeasonId = yearSeasonId
        };

        return this.ToIActionResult(await _mediator.Send(query));
    }
}

