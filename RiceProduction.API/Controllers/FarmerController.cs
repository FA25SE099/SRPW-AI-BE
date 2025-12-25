using Azure.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Services;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.Common.Models.Request.FarmerRequests;
using RiceProduction.Application.Common.Models.Response.FarmerResponses;
using RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;
using RiceProduction.Application.FarmerFeature;
using RiceProduction.Application.FarmerFeature.Command;
using RiceProduction.Application.FarmerFeature.Command.CreateFarmer;
using RiceProduction.Application.FarmerFeature.Command.ImportFarmer;
using RiceProduction.Application.FarmerFeature.Command.UpdateFarmer;
using RiceProduction.Application.FarmerFeature.Commands.ChangeFarmerStatus;
using RiceProduction.Application.FarmerFeature.Queries;
using RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerExcel;
using RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerImportTemplate;
using RiceProduction.Application.FarmerFeature.Queries.ExportFarmerTemplateExcel;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetAll;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmersForAdmin;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Application.PlotFeature.Queries.GetPlotsByFarmer;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Application.ReportFeature.Queries.GetReportsByFarmer;
using RiceProduction.Application.SupervisorFeature.Commands.CreateSupervisor;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System.Security.Claims;
using static RiceProduction.Application.Common.Constants.ApplicationMessages;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmerController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<FarmerController> _logger;
        private readonly IUser _currentUser;
        public FarmerController(IMediator mediator, ILogger<FarmerController> logger, IUser currentUser)
        {
            _mediator = mediator;
            _logger = logger;
            _currentUser = currentUser;
        }
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(FarmerDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]

        public async Task<ActionResult<FarmerDTO>> GetFarmerById(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    return BadRequest("Invalid Farmer ID");
                }

                var query = new GetFarmerByIdQueries(id);
                var result = await _mediator.Send(query);
                if (result == null)
                {
                    return NotFound($"Farmer with ID {id} not found");
                }
                return Ok(result);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting farmer {FarmerId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
        [HttpGet("Detail/{id}")]
        [ProducesResponseType(typeof(FarmerDetailDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<FarmerDetailDTO>> GetFarmerDetailById(Guid id)
        {
            try
            {
                if (id == null)
                {
                    return BadRequest("Invalid Farmer Id");
                }
                var query = new GetFarmerDetailQueries(id);
                var result = await _mediator.Send(query);
                if (result == null)
                {
                    return NotFound($"Farmer detail with ID {id} not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting farmer {FarmerId}", id);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }



        [HttpGet]
         public async Task<ActionResult<PagedResult<IEnumerable<FarmerDTO>>>> GetAllFarmers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] Guid? clusterManagerId = null)
         {
            var query = new GetAllFarmerQueries
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
        [HttpGet("profile")]
        [ProducesResponseType(typeof(FarmerDetailDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<FarmerDetailDTO>> GetFarmerProfile()
        {
            try
            {
                var userId = _currentUser.Id;
                if (userId == null || userId == Guid.Empty)
                {
                    return BadRequest("Invalid User ID");
                }

                var query = new GetFarmerByIdQueries(userId.Value);
                var result = await _mediator.Send(query);
                if (result == null)
                {
                    return NotFound($"Farmer profile for user ID {userId} not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting farmer profile");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpPost("Import")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ImportFarmerResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ImportFarmerResult), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportFarmers([FromForm] FileUploadRequest requestModel)
        {
            if (requestModel.File == null || requestModel.File.Length == 0)
            {
                var errorResult = new ImportFarmerResult
                {
                    Errors = { new ImportError { ErrorMessage = "File không được để trống." } }
                };
                return BadRequest(errorResult);
            }
            
            // Get current cluster manager ID if authenticated
            Guid? clusterManagerId = null;
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId))
            {
                clusterManagerId = userId;
            }
            
            var command = new ImportFarmerCommand(requestModel.File, clusterManagerId);
            var result = await _mediator.Send(command);

            if (result.FailureCount > 0)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("Export-Farmer-BasicData")]
        public async Task<IActionResult> DownloadExcel([FromBody] DateTime request)
        {
            var query = new DownloadAllFarmerExcelQuery
            {
                InputDate = request
            };
            var result = await _mediator.Send(query);
            if (!result.Succeeded || result.Data == null)
            {
                return BadRequest(new { message = result.Message });
            }
            return result.Data;
        }

        [HttpPost("Export-Template-Data")]
        public async Task<IActionResult> ExportTemplate()
        {
            var query = new ExportFarmerTemplateQueries();
            var result = await _mediator.Send(query);
            if (!result.Succeeded || result.Data == null)
            {
                return BadRequest(new { message = result.Message });
            }
            return result.Data;
        }

        [HttpGet("download-import-template")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DownloadFarmerImportTemplate()
        {
            try
            {
                var query = new DownloadFarmerImportTemplateQuery();
                var result = await _mediator.Send(query);

                if (!result.Succeeded || result.Data == null)
                {
                    _logger.LogWarning("Failed to generate farmer import template: {Message}", result.Message);
                    return BadRequest(new { message = result.Message ?? "Failed to generate template" });
                }

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating farmer import template");
                return StatusCode(500, new { message = "An error occurred while generating the template" });
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(FarmerDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateFarmer([FromBody] CreateFarmersCommand command)
        {
            try
            {
                // Ensure user is authenticated and has a valid Guid ID
                if (!_currentUser.Id.HasValue)
                {
                    return Unauthorized("User is not authenticated or has no valid ID.");
                }

                command.ClusterManagerId = _currentUser.Id;

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    return BadRequest(result);
                }

                return Ok(result);     
                    }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating farmer by user {UserId}", _currentUser.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while processing your request.");
            }
        }

        [HttpPut]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFarmer([FromBody] UpdateFarmerRequest request)
        {
            try
            {
                var command = new UpdateFarmerCommand
                {
                    FarmerId = request.FarmerId,
                    FullName = request.FullName,
                    Address = request.Address,
                    FarmCode = request.FarmCode
                };

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating farmer");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
        [HttpPost("create-report")]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<Result<Guid>>> CreateReport([FromForm] CreateEmergencyReportCommand command)
        {
            try
            {
                _logger.LogInformation(
                    "Create emergency report request received: Type={AlertType}, Title={Title}, Severity={Severity}, Images={ImageCount}",
                    command.AlertType, command.Title, command.Severity, command.Images?.Count ?? 0);

                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "Failed to create emergency report: {Errors}",
                        string.Join(", ", result.Errors ?? new string[0]));
                    return BadRequest(result);
                }

                _logger.LogInformation(
                    "Emergency report created successfully with ID: {ReportId}",
                    result.Data);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while creating emergency report");
                return StatusCode(500, Result<Guid>.Failure("An unexpected error occurred"));
            }
        }

        /// <summary>
        /// Get all plots owned by a farmer
        /// </summary>
        [HttpPost("plots")]
        [ProducesResponseType(typeof(PagedResult<List<PlotListResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<List<PlotListResponse>>>> GetPlotsByFarmer(
            [FromBody] GetPlotsByFarmerQuery query)
        {
            try
            {
                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    if (result.Message?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting plots for farmer {FarmerId}", query.FarmerId);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Get all emergency reports for a farmer (sorted by newest first)
        /// </summary>
        [HttpPost("reports")]
        [Authorize(Roles = "Farmer")]
        [ProducesResponseType(typeof(PagedResult<List<ReportItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<List<ReportItemResponse>>>> GetReportsByFarmer(
            [FromBody] GetReportsByFarmerRequest request)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var farmerId))
                {
                    return Unauthorized(PagedResult<List<ReportItemResponse>>.Failure("User not authenticated"));
                }

                var query = new GetReportsByFarmerQuery
                {
                    FarmerId = farmerId,
                    CurrentPage = request.CurrentPage,
                    PageSize = request.PageSize,
                    SearchTerm = request.SearchTerm,
                    Status = request.Status,
                    Severity = request.Severity,
                    ReportType = request.ReportType
                };

                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    if (result.Message?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Change farmer status (Admin only)
        /// </summary>
        [HttpPut("{farmerId}/status")]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ChangeFarmerStatus(
            Guid farmerId,
            [FromBody] ChangeFarmerStatusCommand command)
        {
            try
            {
                var result = await _mediator.Send(command);

                if (!result.Succeeded)
                {
                    if (result.Message?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while changing status for farmer {FarmerId}", farmerId);
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        /// <summary>
        /// Get all farmers with filtering and pagination (Admin)
        /// </summary>
        [HttpPost("get-all")]
        [ProducesResponseType(typeof(PagedResult<List<FarmerListResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<List<FarmerListResponse>>>> GetAllFarmersForAdmin(
            [FromBody] FarmerListRequest request)
        {
            try
            {
                var query = new GetFarmersForAdminQuery
                {
                    CurrentPage = request.CurrentPage,
                    PageSize = request.PageSize,
                    Search = request.Search,
                    PhoneNumber = request.PhoneNumber,
                    ClusterId = request.ClusterId,
                    FarmerStatus = request.FarmerStatus
                };

                var result = await _mediator.Send(query);

                if (!result.Succeeded)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting farmers for admin");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }
    }
}

public class GetReportsByFarmerRequest
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? ReportType { get; set; }
}