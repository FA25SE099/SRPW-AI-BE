using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Infobip.Api.Client.Model;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InfobipController : ControllerBase
    {
        private readonly ILogger<InfobipController> _logger;

        public InfobipController(ILogger<InfobipController> logger)
        {
            _logger = logger;
        }

        [HttpPost("incoming-sms")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ReceiveIncomingSms([FromBody] SmsInboundMessageResult smsInboundMessageResult)
        {
            try
            {
                _logger.LogInformation("Received {Count} incoming SMS messages",
                    smsInboundMessageResult.Results?.Count ?? 0);

                foreach (var result in smsInboundMessageResult.Results ?? Enumerable.Empty<SmsInboundMessage>())
                {
                    _logger.LogInformation("From: {From}, To: {To}, Message: {Message}, ReceivedAt: {ReceivedAt}",
                        result.From,
                        result.To,
                        result.CleanText,
                        result.ReceivedAt);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming SMS");
                return Ok();
            }
        }
    }
}

