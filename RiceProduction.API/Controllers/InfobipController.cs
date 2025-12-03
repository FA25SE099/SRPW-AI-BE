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

        //[HttpPost("delivery-reports")]
        //[AllowAnonymous]
        //[ProducesResponseType(StatusCodes.Status200OK)]
        //public IActionResult ReceiveDeliveryReport([FromBody] SmsDeliveryResult deliveryResult)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Received Infobip delivery report for {Count} messages",
        //            deliveryResult.Results?.Count ?? 0);

        //        foreach (var result in deliveryResult.Results ?? Enumerable.Empty<SmsReport>())
        //        {
        //            _logger.LogInformation("MessageId: {MessageId}, Status: {Status}, To: {To}, SentAt: {SentAt}, DoneAt: {DoneAt}",
        //                result.MessageId,
        //                result.Status?.Name,
        //                result.To,
        //                result.SentAt,
        //                result.DoneAt);
        //        }

        //        return Ok();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error processing Infobip delivery report");
        //        return Ok();
        //    }
        //}

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

