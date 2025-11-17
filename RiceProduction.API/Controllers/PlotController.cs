using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Commands.EditPlot;
using RiceProduction.Application.PlotFeature.Commands.ImportExcel;
using RiceProduction.Application.PlotFeature.Commands.UpdateBoundaryExcel;
using RiceProduction.Application.PlotFeature.Queries;
using RiceProduction.Application.PlotFeature.Queries.DownloadPlotImportTemplate;
using RiceProduction.Application.PlotFeature.Queries.DownloadSample;
using RiceProduction.Application.PlotFeature.Queries.GetAll;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Application.PlotFeature.Queries.GetById;
using RiceProduction.Application.PlotFeature.Queries.GetDetail;
using RiceProduction.Application.PlotFeature.Queries.GetOutOfSeason;
using RiceProduction.Domain.Entities;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlotController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PlotController> _logger;

        private readonly IUser _currentUser;

        public PlotController(IMediator mediator, ILogger<PlotController> logger, IUser currentUser)
        {
            _mediator = mediator;
            _logger = logger;
            _currentUser = currentUser;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PlotDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult<PlotDTO>> GetPlotById(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid Plot ID");
                }
                var query = new GetPlotByIDQueries(id);
                var result = await _mediator.Send(query);
                if (result == null)
                {
                    return NotFound($"Plot with id {id} not found");
                }
                return Ok(result);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting Plot {PlotId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<PlotDTO>>> GetAllPlots(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var query = new GetAllPlotQueries
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm
            };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        
        [HttpPut]
        public async Task<ActionResult<Result<UpdatePlotRequest>>> EditPlot([FromBody] UpdatePlotRequest input)
        {
            var command = new EditPlotCommand
            {
                Request = input
            };
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpGet("detail/{id}")]
        [ProducesResponseType(typeof(Result<PlotDetailDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Result<PlotDetailDTO>>> GetPlotDetail(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest(new { message = "Invalid Plot ID" });
                }
                _logger.LogInformation("Getting plot detail with ID: {PlotId}", id);
                var query = new GetPlotDetailQueries(id);
                var result = await _mediator.Send(query);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to get plot detail: {Message}", result.Message);
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting plot detail {PlotId}", id);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }
        [HttpGet("out-season")]
        [ProducesResponseType(typeof(Result<IEnumerable<PlotDTO>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlotsOutOfSeason ([FromQuery] DateTime? currentDate, [FromQuery] string? searchTerm)
        {
            var query = new GetPlotOutSeasonQueries
            {
                CurrentDate = currentDate,
                SearchTerm = searchTerm
            };
            var result = await _mediator.Send(query);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to get plots out of season: {Message}", result.Message);
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpPost("import-excel")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportPlotsFromExcel(IFormFile excelFile, [FromQuery] DateTime? importDate)
        {
            try
            {
                if (excelFile == null || excelFile.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false, // ✅ Đổi từ 'succeeded' thành 'success'
                        message = "Excel file is required",
                        errors = new[] { "Excel file is required" }
                    });
                }

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var fileExtension = Path.GetExtension(excelFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Only Excel files (.xlsx, .xls) are allowed",
                        errors = new[] { $"Invalid file extension: {fileExtension}" }
                    });
                }

                const int maxFileSize = 10 * 1024 * 1024;
                if (excelFile.Length > maxFileSize)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "File size cannot exceed 10MB",
                        errors = new[] { $"File size: {excelFile.Length} bytes exceeds 10MB" }
                    });
                }

                var command = new ImportPlotByExcelCommand
                {
                    ExcelFile = excelFile,
                    ImportDate = importDate ?? DateTime.UtcNow
                };

                _logger.LogInformation("Starting plot import from Excel file: {FileName}, Size: {FileSize} bytes",
                    excelFile.FileName, excelFile.Length);

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Plot import failed: {Message}", result.Message);

                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        errors = result.Errors ?? new List<string> { result.Message }
                    });
                }

                _logger.LogInformation("Plot import completed successfully. {Count} plots imported",
                    result.Data?.Count ?? 0);

                return Ok(new
                {
                    success = true, 
                    message = result.Message,
                    data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while importing plots from Excel file: {FileName}",
                    excelFile?.FileName ?? "Unknown");

                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing your request",
                    errors = new[] { ex.Message }
                });
            }
        }
        [HttpGet("download-sample-excel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DownloadPlotSampleExcel()
        {
            try
            {
                var query = new DownloadPlotSampleExcelQuery();
                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to generate sample Excel: {Message}", result.Message);
                    return BadRequest(result);
                }

                return result.Data ?? BadRequest(new { message = "Failed to generate Excel file" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating sample Excel");
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        [HttpGet("download-import-template")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DownloadPlotImportTemplate()
        {
            try
            {
                // Get current cluster manager ID if authenticated
                Guid? clusterManagerId = null;
                var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId))
                {
                    clusterManagerId = userId;
                }

                var query = new DownloadPlotImportTemplateQuery 
                { 
                    ClusterManagerId = clusterManagerId 
                };
                
                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to generate plot import template: {Message}", result.Message);
                    return BadRequest(result);
                }

                return result.Data ?? BadRequest(new { message = "Failed to generate template" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating plot import template");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }
        [HttpGet("get-current-farmer-plots")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCurrentFarmerPlots([FromQuery] GetByFarmerIdQuery query)
        {
            if (!_currentUser.Id.HasValue || _currentUser.Id == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid Farmer ID" });
            }
            query.FarmerId = _currentUser.Id.Value;
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to retrieve plots for farmer {FarmerId}: {Message}", _currentUser.Id, result.Message);
                return BadRequest(result);
            }

            return Ok(result.Data);
        }

        [HttpPut("boundaries/import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdatePlotBoundaries(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please upload a valid Excel file"
                });
            }
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid file format. Please upload an Excel file (.xlsx or .xls)"
                });
            }
            const long maxFileSize = 10 * 1024 * 1024; 
            if (file.Length > maxFileSize)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "File size exceeds the maximum allowed size of 10MB"
                });
            }

            var command = new UpdatePlotBoundaryCommand
            {
                ExcelFile = file,
                ImportDate = DateTime.UtcNow
            };

            var result = await _mediator.Send(command);

            if (result.Succeeded)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    data = result.Data,
                    timestamp = DateTime.UtcNow
                });
            }

            return BadRequest(new
            {
                success = false,
                message = result.Message,
                errors = result.Errors,
                timestamp = DateTime.UtcNow
            });
        }
        

    }
}
