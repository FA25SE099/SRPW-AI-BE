using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.ZaloFeature.Commands.ExchangeAuthCode;
using RiceProduction.Application.ZaloFeature.Commands.GenerateAuthUrl;
using RiceProduction.Application.ZaloFeature.Commands.RefreshToken;
using RiceProduction.Application.ZaloFeature.Commands.SendZns;
using RiceProduction.Application.ZaloFeature.Commands.SendBulkZns;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZaloController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<ZaloController> _logger;

    public ZaloController(ISender sender, ILogger<ZaloController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Generate Zalo OAuth authorization URL and code verifier
    /// </summary>
    /// <param name="redirectUri">Optional custom redirect URI</param>
    /// <returns>Authorization URL, code verifier, and state</returns>
    [HttpGet("auth/url")]
    public async Task<IActionResult> GetAuthorizationUrl([FromQuery] string? redirectUri = null)
    {
        var command = new GenerateAuthUrlCommand
        {
            RedirectUri = redirectUri ?? string.Empty
        };
        
        var result = await _sender.Send(command);
        
        return Ok(new
        {
            authorizationUrl = result.AuthorizationUrl,
            codeVerifier = result.CodeVerifier,
            state = result.State,
            message = "Please save the codeVerifier. You will need it to exchange the authorization code for tokens."
        });
    }

    /// <summary>
    /// Exchange authorization code for access token and refresh token
    /// </summary>
    /// <param name="command">Authorization code and code verifier</param>
    /// <returns>Access token, refresh token, and expiration time</returns>
    [HttpPost("auth/token")]
    public async Task<IActionResult> ExchangeAuthCode([FromBody] ExchangeAuthCodeCommand command)
    {
        try
        {
            var result = await _sender.Send(command);
            
            return Ok(new
            {
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresIn = result.ExpiresIn,
                message = "Access token obtained successfully. Token expires in " + result.ExpiresIn + " seconds (approximately 25 hours)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="command">Refresh token</param>
    /// <returns>New access token, refresh token, and expiration time</returns>
    [HttpPost("auth/refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshZaloTokenCommand command)
    {
        try
        {
            var result = await _sender.Send(command);
            
            return Ok(new
            {
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                expiresIn = result.ExpiresIn,
                message = "Access token refreshed successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send Zalo Notification Service (ZNS) message
    /// </summary>
    /// <param name="command">ZNS request with phone, template ID, template data, and access token</param>
    /// <returns>ZNS response with message ID and quota information</returns>
    [HttpPost("zns/send")]
    public async Task<IActionResult> SendZns([FromBody] SendZnsCommand command)
    {
        try
        {
            var result = await _sender.Send(command);
            
            if (result.Error != 0)
            {
                return BadRequest(new
                {
                    error = result.Error,
                    message = result.Message,
                    data = result.Data
                });
            }

            return Ok(new
            {
                success = true,
                messageId = result.Data?.MsgId,
                sentTime = result.Data?.SentTime,
                sendingMode = result.Data?.SendingMode,
                quota = result.Data?.Quota,
                message = "ZNS sent successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending ZNS");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send bulk ZNS notifications with concurrent processing and retry logic
    /// </summary>
    /// <param name="command">Bulk send request with list of ZNS messages</param>
    /// <returns>Summary of bulk send operation including success/failure counts and detailed results</returns>
    [HttpPost("zns/bulk-send")]
    public async Task<IActionResult> SendBulkZns([FromBody] SendBulkZnsCommand command)
    {
        try
        {
            var result = await _sender.Send(command);
            
            return Ok(new
            {
                success = true,
                summary = new
                {
                    totalRequests = result.TotalRequests,
                    successCount = result.SuccessCount,
                    failedCount = result.FailedCount,
                    totalDuration = result.TotalDuration.TotalSeconds,
                    startTime = result.StartTime,
                    endTime = result.EndTime
                },
                results = result.Results.Select(r => new
                {
                    trackingId = r.TrackingId,
                    phone = r.Phone,
                    success = r.Success,
                    messageId = r.Response?.Data?.MsgId,
                    errorMessage = r.ErrorMessage,
                    retryCount = r.RetryCount
                }),
                message = $"Bulk send completed: {result.SuccessCount} succeeded, {result.FailedCount} failed out of {result.TotalRequests} total requests"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk ZNS");
            return BadRequest(new { error = ex.Message });
        }
    }
}
