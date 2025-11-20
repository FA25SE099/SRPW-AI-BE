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

        public TestController(IStorageService storageService, ILogger<TestController> logger)
        {
            _storageService = storageService;
            _logger = logger;
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