using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.SmsFeature.Commands.TestSendSms;
using RiceProduction.Application.SmsFeature.Commands.ProcessSmsDeliveryWebhook;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SmsController> _logger;

    public SmsController(IMediator mediator, ILogger<SmsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Test SMS sending functionality
    /// </summary>
    /// <param name="command">Test SMS command with phone number, message, and recipient ID</param>
    /// <returns>Result of SMS sending operation with notification ID</returns>
    [HttpPost("test")]
    [ProducesResponseType(typeof(TestSendSmsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TestSendSmsResponse>> TestSendSms([FromBody] TestSendSmsCommand command)
    {
        try
        {
            _logger.LogInformation("Received test SMS request for phone: {PhoneNumber}", command.PhoneNumber);

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                _logger.LogWarning("SMS test failed for phone: {PhoneNumber}. Error: {Error}", 
                    command.PhoneNumber, result.ErrorMessage);
                return BadRequest(result);
            }

            _logger.LogInformation("SMS test successful. NotificationId: {NotificationId}, MessageId: {MessageId}", 
                result.NotificationId, result.MessageId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while testing SMS");
            return StatusCode(500, new TestSendSmsResponse
            {
                Success = false,
                Status = "error",
                ErrorMessage = "An unexpected error occurred while sending SMS"
            });
        }
    }

    /// <summary>
    /// Webhook endpoint to receive delivery status updates from SpeedSMS
    /// </summary>
    /// <param name="webhookRequest">Webhook data from SpeedSMS</param>
    /// <returns>Webhook processing result</returns>
    [HttpPost("webhook/delivery")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebhookResponse>> ReceiveDeliveryWebhook([FromBody] SpeedSmsWebhookRequest webhookRequest)
    {
        try
        {
            _logger.LogInformation("Received SpeedSMS webhook - Type: {Type}, TranId: {TranId}, Phone: {Phone}, Status: {Status}",
                webhookRequest.Type, webhookRequest.TranId, webhookRequest.Phone, webhookRequest.Status);

            var command = new ProcessSmsDeliveryWebhookCommand
            {
                Type = webhookRequest.Type,
                TranId = webhookRequest.TranId,
                Phone = webhookRequest.Phone,
                Status = webhookRequest.Status
            };

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                _logger.LogWarning("Webhook processing failed: {Message}", result.Message);
            }

            // Always return 200 OK to SpeedSMS to acknowledge receipt
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SpeedSMS webhook");
            
            // Still return 200 OK to prevent SpeedSMS from retrying
            return Ok(new WebhookResponse
            {
                Success = false,
                Message = "Webhook received but processing failed"
            });
        }
    }
}
