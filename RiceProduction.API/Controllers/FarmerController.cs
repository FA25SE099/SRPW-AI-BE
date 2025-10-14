using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request;
using RiceProduction.Application.FarmerFeature;
using RiceProduction.Application.FarmerFeature.Command;
using RiceProduction.Application.FarmerFeature.Queries;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;

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
        [HttpGet]
         public async Task<ActionResult<PagedResult<IEnumerable<FarmerDTO>>>> GetAllFarmers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
         {
            var query = new GetAllFarmerQueries
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
        [HttpPost("import")]
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
            var command = new ImportFarmerCommand(requestModel.File);
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


    }
    }
