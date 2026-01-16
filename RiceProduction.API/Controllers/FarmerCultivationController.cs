using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.FarmerFeature.Commands.SelectCultivationPreferences;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmerCultivationSelections;
using RiceProduction.Application.FarmerFeature.Queries.ValidateCultivationPreferences;
using RiceProduction.Application.SeasonFeature.Queries.GetAvailableRiceVarietiesForSeason;
using RiceProduction.Domain.Entities;

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
    private readonly IUnitOfWork _unitOfWork;

    public FarmerCultivationController(
        IMediator mediator,
        ILogger<FarmerCultivationController> logger,
        IUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _mediator = mediator;
        _logger = logger;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
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
        try
        {
            var query = new GetAvailableRiceVarietiesForSeasonQuery
            {
                SeasonId = seasonId,
                OnlyRecommended = onlyRecommended
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
            _logger.LogError(ex, "Error retrieving available varieties for season {SeasonId}", seasonId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving available varieties" });
        }
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
        try
        {
            // Get the YearSeason to extract the SeasonId
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .FindAsync(ys => ys.Id == yearSeasonId);

            if (yearSeason == null)
            {
                return NotFound(new { message = "Year Season not found" });
            }

            var query = new GetAvailableRiceVarietiesForSeasonQuery
            {
                SeasonId = yearSeason.SeasonId,
                OnlyRecommended = onlyRecommended
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
            _logger.LogError(ex, "Error retrieving available varieties for year season {YearSeasonId}", yearSeasonId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving available varieties" });
        }
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
        try
        {
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating cultivation preferences");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while validating your preferences" });
        }
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
        try
        {
            // Additional authorization check: ensure farmer owns the plot
            if (_currentUser.Id.HasValue)
            {
                var plot = await _unitOfWork.Repository<Plot>()
                    .FindAsync(p => p.Id == command.PlotId);

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

            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting cultivation preferences");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while saving your selection" });
        }
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
        try
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

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving farmer selections");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving your selections" });
        }
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
        try
        {
            var query = new GetFarmerCultivationSelectionsQuery
            {
                FarmerId = farmerId,
                YearSeasonId = yearSeasonId
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
            _logger.LogError(ex, "Error retrieving farmer {FarmerId} selections", farmerId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving farmer selections" });
        }
    }
}

