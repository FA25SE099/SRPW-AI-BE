using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Auth.Commands.ChangePassword;
using RiceProduction.Application.Auth.Commands.ForgotPassword;
using RiceProduction.Application.Auth.Commands.Login;
using RiceProduction.Application.Auth.Commands.Logout;
using RiceProduction.Application.Auth.Commands.RefreshToken;
using RiceProduction.Application.Auth.Commands.ResetPassword;
using RiceProduction.Application.Auth.Queries.GetUserById;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Response;

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
            PhoneNumber = request.PhoneNumber,
            Password = request.Password,
            RememberMe = request.RememberMe ?? true
        };

        return this.ToActionResult(await _mediator.Send(command));
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
        (string Email, string Password)? credentials = role switch
        {
            TestRole.Admin => ("admin@ricepro.com", "Admin123!"),
            TestRole.Supervisor => ("supervisor1@ricepro.com", "Super123!"),
            TestRole.Expert => ("expert1@ricepro.com", "Expert123!"),
            TestRole.Uav => ("user@ricepro.com", "User123!"),
            TestRole.ClusterManager => ("cluster1@ricepro.com", "Manager123!"),
            _ => null
        };

        if (credentials is null)
        {
            return BadRequest(new { Error = "Invalid role specified. Supported roles: admin, supervisor, expert, uav, clustermanager." });
        }

        var command = new LoginCommand
        {
            Email = credentials.Value.Email,
            Password = credentials.Value.Password,
            RememberMe = rememberMe ?? true
        };

        return this.ToActionResult(await _mediator.Send(command));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<Result<LogoutResponse>>> Logout([FromBody] LogoutRequest? request = null)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(Result<LogoutResponse>.Failure("User not authenticated"));
        }

        var command = new LogoutCommand
        {
            UserId = userId,
            RefreshToken = request?.RefreshToken
        };

        return this.ToActionResult(await _mediator.Send(command));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<Result<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshTokenCommand
        {
            AccessToken = request.AccessToken,
            RefreshToken = request.RefreshToken
        };

        return this.ToActionResult(await _mediator.Send(command));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<Result<UserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var query = new GetUserByIdQuery { UserId = userId };
        return await _mediator.Send(query);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<Result>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var command = new ChangePasswordCommand
        {
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword
        };

        return ToActionResult(await _mediator.Send(command));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<Result>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand
        {
            Email = request.Email,
            Token = request.Token,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword
        };

        return ToActionResult(await _mediator.Send(command));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<Result>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var command = new ForgotPasswordCommand
        {
            Email = request.Email
        };

        return ToActionResult(await _mediator.Send(command));
    }

    private ActionResult<Result> ToActionResult(Result result) =>
        result.Succeeded ? Ok(result) : BadRequest(result);
}