using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.NotificationFeature.Command.GetPushToken;
using RiceProduction.Application.NotificationFeature.Command.PushNotification;

namespace RiceProduction.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(IMediator mediator, ILogger<NotificationController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }
        [HttpPost("send-push")]
        public async Task<IActionResult> SendPushNotification([FromBody] SendPushNotificationCommand command)
        {
            try
            {
                var result = await _mediator.Send(command);
                if (!result.Success)
                {
                    _logger.LogWarning("Push notification failed: {ErrorMessage}", result.ErrorMessage);
                    return BadRequest(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending push notification");
                return StatusCode(500, new { message = "An error occurred while processing your request", error = ex.Message });
            }
        }
        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterPushToken([FromBody] RegisterPushTokenCommand command)
        {
            try
            {
                var result = await _mediator.Send(command);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Push token registration failed: {ErrorMessage}", result.Message);
                    return BadRequest(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while registering push token");
                return StatusCode(500, new { message = "An error occurred while processing your request", error = ex.Message });
            }
        }
    }
}
