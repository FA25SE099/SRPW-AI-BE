using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces.External;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {

        private readonly IStorageService _storageService;
        private readonly ILogger<TestController> _logger;
        private readonly ISmSService _smsService;

        public TestController(IStorageService storageService, ILogger<TestController> logger, ISmSService smsService)
        {
            _storageService = storageService;
            _logger = logger;
            _smsService = smsService;
        }

        [HttpPost("upload-files")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> TestUploadFiles([FromForm] UploadMultiples request)
        {
            try
            {
                var files = request.Files;
                if (files == null || files.Count == 0)
                {
                    return BadRequest("No files provided");
                }

                var results = await _storageService.UploadMultipleAsync(files);

                return Ok(new
                {
                    Success = true,
                    Count = results.Count,
                    Files = results.Select(r => new
                    {
                        Url = r.Url,
                        FileName = r.FileName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("upload-single-file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> TestUploadSingleFile([FromForm] UploadSingleFileRequest request)
        {
            try
            {
                var file = request.File;
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file provided");
                }

                var result = await _storageService.UploadAsync(file);

                return Ok(new
                {
                    Success = true,
                    Url = result.Url,
                    FileName = result.FileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpDelete("delete-file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> TestDeleteFile([FromQuery] string fileName, [FromQuery] string? folder = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return BadRequest("File name is required");
                }

                var deleted = await _storageService.DeleteAsync(fileName, folder);

                return Ok(new
                {
                    Success = deleted,
                    Message = deleted ? "File deleted successfully" : "File not found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("get-sas-url")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> TestGetSasUrl([FromQuery] string fileName, [FromQuery] string? folder = null, [FromQuery] int expiryMinutes = 30)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return BadRequest("File name is required");
                }

                var sasUrl = await _storageService.GetSasUrlAsync(fileName, folder, TimeSpan.FromMinutes(expiryMinutes));

                return Ok(new
                {
                    Success = true,
                    SasUrl = sasUrl,
                    ExpiresIn = $"{expiryMinutes} minutes"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS URL");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("sms/send")]
        public async Task<ActionResult<object>> TestSendSms([FromBody] TestSmsRequest request)
        {
            try
            {
                _logger.LogInformation("Testing SMS send to: {Phones}", string.Join(", ", request.PhoneNumbers));

                if (request.PhoneNumbers == null || request.PhoneNumbers.Length == 0)
                {
                    return BadRequest(new { Success = false, Error = "Phone numbers are required" });
                }

                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { Success = false, Error = "Content is required" });
                }

                var result =  _smsService.sendSMS(
                    request.PhoneNumbers,
                    request.Content,
                    (int)request.Type,
                    request.Sender
                );

                return Ok(new
                {
                    Success = true,
                    Response = result,
                    SentTo = request.PhoneNumbers,
                    Type = request.Type ?? 2,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("sms/user-info")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<object> TestGetUserInfo()
        {
            try
            {
                _logger.LogInformation("Getting SMS user info");

                var result = _smsService.getUserInfo();

                return Ok(new
                {
                    Success = true,
                    UserInfo = result,
                    Message = "User info retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("sms/send-mms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<object> TestSendMms([FromBody] TestMmsRequest request)
        {
            try
            {
                _logger.LogInformation("Testing MMS send to: {Phones}", string.Join(", ", request.PhoneNumbers));

                if (request.PhoneNumbers == null || request.PhoneNumbers.Length == 0)
                {
                    return BadRequest(new { Success = false, Error = "Phone numbers are required" });
                }

                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { Success = false, Error = "Content is required" });
                }

                if (string.IsNullOrEmpty(request.Link))
                {
                    return BadRequest(new { Success = false, Error = "Link is required for MMS" });
                }

                var result = _smsService.sendMMS(
                    request.PhoneNumbers,
                    request.Content,
                    request.Link,
                    request.Sender ?? ""
                );

                return Ok(new
                {
                    Success = true,
                    Response = result,
                    SentTo = request.PhoneNumbers,
                    Message = "MMS sent successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending MMS");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("infobip/send")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> TestInfobipSendSms([FromBody] TestSmsRequest request)
        {
            try
            {
                _logger.LogInformation("Testing Infobip SMS send to: {Phones}", string.Join(", ", request.PhoneNumbers));

                if (request.PhoneNumbers == null || request.PhoneNumbers.Length == 0)
                {
                    return BadRequest(new { Success = false, Error = "Phone numbers are required" });
                }

                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { Success = false, Error = "Content is required" });
                }

                var result = await _smsService.SendSMSAsync(
                    request.PhoneNumbers,
                    request.Content,
                    request.Type ?? 2,
                    request.Sender ?? ""
                );

                return Ok(new
                {
                    Success = true,
                    Response = result,
                    SentTo = request.PhoneNumbers,
                    Message = "Infobip SMS sent"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Infobip SMS");
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }
    }
}
public class UploadSingleFileRequest
{
    public IFormFile File { get; set; }  
}
public class UploadMultiples
{
    public List<IFormFile> Files { get; set; }
}
public class TestSmsRequest
{
    public string[] PhoneNumbers { get; set; }
    public string Content { get; set; }
    public int? Type { get; set; }
    public string? Sender { get; set; }
}

public class TestMmsRequest
{
    public string[] PhoneNumbers { get; set; }
    public string Content { get; set; }
    public string Link { get; set; }
    public string? Sender { get; set; }
}