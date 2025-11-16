using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Auth.Commands.Login;
using RiceProduction.Application.Auth.Commands.Logout;
using RiceProduction.Application.Auth.Commands.RefreshToken;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;
using System.Security.Claims;
using RiceProduction.Application.Auth.Queries.GetUserById;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<ActionResult<Result<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand
        {
            Email = request.Email,
            Password = request.Password,
            RememberMe = request.RememberMe ?? true
        };

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    public enum TestRole
    {
        Admin,
        Supervisor,
        Expert,
        Uav,
        ClusterManager
    }
    [HttpGet("login-fast")]
    public async Task<ActionResult<Result<LoginResponse>>> LoginFast([FromQuery] TestRole role, [FromQuery] bool? rememberMe = true)
    {
        if (!Enum.IsDefined(typeof(TestRole), role))
        {
            return BadRequest(new { Error = "Invalid role specified. Supported roles: admin, supervisor, expert, uav, clustermanager." });
        }

        string email = null;
        string password = null;

        switch (role)
        {
            case TestRole.Admin:
                email = "admin@ricepro.com";
                password = "Admin123!";
                break;
            case TestRole.Supervisor:
                email = "supervisor1@ricepro.com";
                password = "Super123!";
                break;
            case TestRole.Expert:
                email = "expert1@ricepro.com";
                password = "Expert123!";
                break;
            case TestRole.Uav:
                email = "user@ricepro.com";
                password = "User123!";
                break;
            case TestRole.ClusterManager:
                email = "cluster1@ricepro.com";
                password = "Manager123!";
                break;
            default:
                return BadRequest(new { Error = "Invalid role specified." });
        }

        var command = new LoginCommand
        {
            Email = email,
            Password = password,
            RememberMe = rememberMe ?? true
        };

        var result = await _mediator.Send(command);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<Result<LogoutResponse>>> Logout([FromBody] LogoutRequest? request = null)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(Result<LogoutResponse>.Failure("User not authenticated"));
        }

        var command = new LogoutCommand
        {
            UserId = userId,
            RefreshToken = request?.RefreshToken
        };

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<Result<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshTokenCommand
        {
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken
        };

        var result = await _mediator.Send(command);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
    [HttpGet("me")]
    [Authorize] 
    public async Task<Result<UserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var query = new GetUserByIdQuery(){UserId = userId};
        var user = await _mediator.Send(query);

        return user;
    }
}