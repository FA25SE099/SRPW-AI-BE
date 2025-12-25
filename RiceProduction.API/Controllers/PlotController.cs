using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequests;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.PlotFeature.Commands.CreatePlot;
using RiceProduction.Application.PlotFeature.Commands.CreatePlots;
using RiceProduction.Application.PlotFeature.Commands.EditPlot;
using RiceProduction.Application.PlotFeature.Commands.ImportExcel;
using RiceProduction.Application.PlotFeature.Commands.UpdateBoundaryExcel;
using RiceProduction.Application.PlotFeature.Commands.UpdateCoordinate;
using RiceProduction.Application.PlotFeature.Queries;
using RiceProduction.Application.PlotFeature.Queries.CheckPlotPolygonEditable;
using RiceProduction.Application.PlotFeature.Queries.DownloadPlotImportTemplate;
using RiceProduction.Application.PlotFeature.Queries.DownloadSample;
using RiceProduction.Application.PlotFeature.Queries.ExportPlotData;
using RiceProduction.Application.PlotFeature.Queries.GetAll;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Application.PlotFeature.Queries.GetById;
using RiceProduction.Application.PlotFeature.Queries.GetDetail;
using RiceProduction.Application.PlotFeature.Queries.GetOutOfSeason;
using RiceProduction.Application.PlotFeature.Queries.GetPlotsAwaitingPolygon;
using RiceProduction.Domain.Entities;
using static RiceProduction.Application.PlotFeature.Commands.UpdateCoordinate.UpdateCoordinateCommand;

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
            [FromQuery] string? searchTerm = null,
            [FromQuery] Guid? clusterManagerId = null)
        {
            var query = new GetAllPlotQueries
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                ClusterManagerId = clusterManagerId
            };
            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        
        [HttpPost]
        [ProducesResponseType(typeof(Result<PlotResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePlot([FromBody] CreatePlotCommand command)
        {
            try
            {
                _logger.LogInformation(
                    "Create plot request received for farmer {FarmerId}: SoThua={SoThua}, SoTo={SoTo}",
                    command.FarmerId, command.SoThua, command.SoTo);

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to create plot: {Errors}", 
                        string.Join(", ", result.Errors ?? new string[0]));
                    return BadRequest(result);
                }

                _logger.LogInformation("Plot created successfully: {PlotId}", result.Data?.PlotId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating plot");
                return StatusCode(500, Result<PlotResponse>.Failure("An error occurred while processing your request"));
            }
        }

        [HttpPost("bulk")]
        [ProducesResponseType(typeof(Result<List<PlotResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePlots([FromBody] CreatePlotsCommand command)
        {
            try
            {
                _logger.LogInformation(
                    "Bulk plot creation request received for {Count} plots",
                    command.Plots?.Count ?? 0);

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to create plots in bulk: {Errors}", 
                        string.Join(", ", result.Errors ?? new string[0]));
                    return BadRequest(result);
                }

                _logger.LogInformation(
                    "Bulk plot creation successful: {Count} plots created",
                    result.Data?.Count ?? 0);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating plots in bulk");
                return StatusCode(500, Result<List<PlotResponse>>.Failure("An error occurred while processing your request"));
            }
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

        [HttpGet("awaiting-polygon")]
        [ProducesResponseType(typeof(PagedResult<IEnumerable<PlotAwaitingPolygonDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPlotsAwaitingPolygon(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] Guid? clusterManagerId = null,
            [FromQuery] Guid? supervisorId = null,
            [FromQuery] bool? hasActiveTask = null,
            [FromQuery] string? taskStatus = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? sortBy = "DaysWaiting",
            [FromQuery] bool descending = true)
        {
            var query = new GetPlotsAwaitingPolygonQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                ClusterId = clusterId,
                ClusterManagerId = clusterManagerId,
                SupervisorId = supervisorId,
                HasActiveTask = hasActiveTask,
                TaskStatus = taskStatus,
                SearchTerm = searchTerm,
                SortBy = sortBy,
                Descending = descending
            };

            var result = await _mediator.Send(query);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to get plots awaiting polygon: {Message}", result.Message);
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

        [HttpGet("export-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportPlotData(
            [FromQuery] Guid? clusterManagerId = null,
            [FromQuery] Guid? farmerId = null,
            [FromQuery] Guid? groupId = null,
            [FromQuery] bool onlyWithPolygons = false,
            [FromQuery] bool onlyWithoutPolygons = false)
        {
            try
            {
                // If no cluster manager ID provided, try to get from current user
                if (!clusterManagerId.HasValue)
                {
                    var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId))
                    {
                        clusterManagerId = userId;
                    }
                }

                var query = new ExportPlotDataQuery
                {
                    ClusterManagerId = clusterManagerId,
                    FarmerId = farmerId,
                    GroupId = groupId,
                    OnlyWithPolygons = onlyWithPolygons,
                    OnlyWithoutPolygons = onlyWithoutPolygons
                };

                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to export plot data: {Message}", result.Message);
                    return BadRequest(new { message = result.Message });
                }

                return result.Data ?? BadRequest(new { message = "Failed to export plot data" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting plot data");
                return StatusCode(500, new { message = "An error occurred while exporting plot data" });
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
        [HttpPut("{plotId}/coordinate")]
        [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Result<bool>>> UpdatePlotCoordinate(
    Guid plotId,
    [FromBody] UpdateCoordinateRequest request)
        {
            try
            {
                if (plotId == Guid.Empty)
                {
                    return BadRequest(Result<bool>.Failure("Invalid Plot ID"));
                }

                if (request == null || string.IsNullOrWhiteSpace(request.CoordinateGeoJson))
                {
                    return BadRequest(Result<bool>.Failure("Coordinate GeoJSON is required"));
                }

                var command = new UpdateCoordinateCommand
                {
                    PlotId = plotId,
                    CoordinateGeoJson = request.CoordinateGeoJson,
                    Notes = request.Notes
                };

                _logger.LogInformation(
                    "Updating coordinate for Plot {PlotId}",
                    plotId);

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "Failed to update coordinate for Plot {PlotId}: {Message}",
                        plotId,
                        result.Message);
                    return BadRequest(result);
                }

                _logger.LogInformation(
                    "Successfully updated coordinate for Plot {PlotId}",
                    plotId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while updating coordinate for Plot {PlotId}",
                    plotId);
                return StatusCode(500, Result<bool>.Failure("An error occurred while processing your request"));
            }
        }

        [HttpGet("check-polygon-editable")]
        [ProducesResponseType(typeof(Result<CheckPlotPolygonEditableResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Result<CheckPlotPolygonEditableResponse>>> CheckPlotPolygonEditable(
            [FromQuery] Guid plotId,
            [FromQuery] Guid yearSeasonId)
        {
            try
            {
                if (plotId == Guid.Empty)
                {
                    return BadRequest(Result<CheckPlotPolygonEditableResponse>.Failure("Invalid Plot ID"));
                }

                if (yearSeasonId == Guid.Empty)
                {
                    return BadRequest(Result<CheckPlotPolygonEditableResponse>.Failure("Invalid Year Season ID"));
                }

                var query = new CheckPlotPolygonEditableQuery
                {
                    PlotId = plotId,
                    YearSeasonId = yearSeasonId
                };

                _logger.LogInformation(
                    "Checking if plot polygon is editable for PlotId: {PlotId}, YearSeasonId: {YearSeasonId}",
                    plotId,
                    yearSeasonId);

                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "Failed to check plot polygon editability for PlotId: {PlotId}: {Message}",
                        plotId,
                        result.Message);
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while checking plot polygon editability for PlotId: {PlotId}",
                    plotId);
                return StatusCode(500, Result<CheckPlotPolygonEditableResponse>.Failure("An error occurred while processing your request"));
            }
        }

    }
}
