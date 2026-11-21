using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.SystemSettingRequests;
using RiceProduction.Application.SystemSettingFeature.Commands.UpdateSystemSetting;
using RiceProduction.Application.SystemSettingFeature.Queries.GetAllSystemSettings;
using RiceProduction.Application.SystemSettingFeature.Queries.GetSystemSettingById;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemSettingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SystemSettingController> _logger;

    public SystemSettingController(IMediator mediator, ILogger<SystemSettingController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all system settings with pagination and filtering
    /// </summary>
    /// <param name="request">Pagination and filter parameters</param>
    /// <returns>Paged list of system settings</returns>
    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<SystemSettingResponse>>>> GetAllSystemSettings(
        [FromBody] SystemSettingListRequest request)
    {
        var query = new GetAllSystemSettingsQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            SearchKey = request.SearchKey,
            Category = request.Category
        };

        return this.ToActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Get a specific system setting by ID
    /// </summary>
    /// <param name="id">System setting ID</param>
    /// <returns>System setting details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<Result<SystemSettingResponse>>> GetSystemSettingById(Guid id)
    {
        var query = new GetSystemSettingByIdQuery { SettingId = id };
        var result = await _mediator.Send(query);

        return result.Succeeded ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Update a system setting (Admin only)
    /// </summary>
    /// <param name="id">System setting ID</param>
    /// <param name="request">Updated setting value and description</param>
    /// <returns>Updated setting ID</returns>
    [HttpPut("{id}")]
    public async Task<ActionResult<Result<Guid>>> UpdateSystemSetting(
        Guid id,
        [FromBody] UpdateSystemSettingRequest request)
    {
        var command = new UpdateSystemSettingCommand
        {
            SettingId = id,
            SettingValue = request.SettingValue,
            SettingDescription = request.SettingDescription
        };

        return this.ToActionResult(await _mediator.Send(command));
    }

    /// <summary>
    /// Get all available setting categories
    /// </summary>
    /// <returns>List of unique categories</returns>
    [HttpGet("categories")]
    public async Task<ActionResult<Result<List<string>>>> GetCategories()
    {
        // Get all settings and extract unique categories
        var query = new GetAllSystemSettingsQuery
        {
            CurrentPage = 1,
            PageSize = 1000 // Get all for categories
        };

        var result = await _mediator.Send(query);

        if (!result.Succeeded || result.Data == null)
        {
            return BadRequest(result);
        }

        var categories = result.Data
            .Select(s => s.SettingCategory)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Ok(Result<List<string>>.Success(categories, "Successfully retrieved categories"));
    }
}

