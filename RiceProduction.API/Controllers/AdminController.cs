using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.AdminFeature.Queries.GetAllUsers;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.AdminRequests;
using RiceProduction.Application.Common.Models.Response.AdminResponses;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : Controller
{
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMediator mediator, ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all users with pagination and filtering
    /// </summary>
    /// <param name="request">Pagination and filter parameters</param>
    /// <returns>Paged list of users with their role</returns>
    [HttpPost("users")]
    public async Task<ActionResult<PagedResult<List<UserResponse>>>> GetAllUsers([FromBody] GetAllUsersRequest request)
    {
        try
        {
            var query = new GetAllUsersQuery
            {
                CurrentPage = request.CurrentPage,
                PageSize = request.PageSize,
                SearchEmailAndName = request.SearchEmailAndName,
                SearchPhoneNumber = request.SearchPhoneNumber,
                Role = request.Role,
                IsActive = request.IsActive,
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
            _logger.LogError(ex, "Error occurred while getting users");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
