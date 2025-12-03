using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.EmergencyReportFeature.Commands.CreateEmergencyReport;
using RiceProduction.Application.FarmerFeature;
using RiceProduction.Application.FarmerFeature.Command;
using RiceProduction.Application.FarmerFeature.Command.CreateFarmer;
using RiceProduction.Application.FarmerFeature.Command.ImportFarmer;
using RiceProduction.Application.FarmerFeature.Command.UpdateFarmer;
using RiceProduction.Application.FarmerFeature.Queries;
using RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerExcel;
using RiceProduction.Application.FarmerFeature.Queries.DownloadFarmerImportTemplate;
using RiceProduction.Application.FarmerFeature.Queries.ExportFarmerTemplateExcel;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetAll;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetById;
using RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetDetailById;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Domain.Entities;
using RiceProduction.Application.SupervisorFeature.Commands.CreateSupervisor;

namespace RiceProduction.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FarmerController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<FarmerController> _logger;
        public FarmerController(IMediator mediator, ILogger<FarmerController> logger)
        {
            _mediator = mediator;
            _logger = logger;
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
        [HttpPost]
        [ProducesResponseType(typeof(FarmerDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateFarmer([FromBody] CreateFarmersCommand command)
        {
            try { 
            var result = await _mediator.Send(command);

            if (!result.Succeeded)
            {
                return BadRequest(result);
            }

            return Ok(result);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating farmer");
                return StatusCode(500, "An error occurred while processing your request");
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
        public async Task<ActionResult<Result<Guid>>> CreateReport([FromBody] CreateEmergencyReportCommand command)
        {
            try
            {
                _logger.LogInformation(
                    "Create emergency report request received: Type={AlertType}, Title={Title}, Severity={Severity}, Images={ImageCount}",
                    command.AlertType, command.Title, command.Severity, command.ImageUrls?.Count ?? 0);

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
    }
    }
