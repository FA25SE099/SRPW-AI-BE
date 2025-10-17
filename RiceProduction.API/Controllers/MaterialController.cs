using Azure.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Commands.ImportCreateAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace RiceProduction.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaterialController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("get-all")]
    public async Task<ActionResult<PagedResult<List<MaterialResponse>>>> AllMaterialPaging([FromBody] MaterialListRequest request)
    {
        var query = new GetAllMaterialByTypeQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            MaterialType = request.Type
        };
        var result = await _mediator.Send(query);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    [HttpPost("download-excel")]
    public async Task<IActionResult> DownloadMaterialExcel([FromBody] DateTime request)
    {
        var query = new DownloadAllMaterialExcelQuery
        {
            InputDate = request
        };
        var result =await _mediator.Send(query);
        if (!result.Succeeded || result.Data == null)
        {
            return BadRequest(new { message = result.Message });
        }
        return result.Data;
    }
    [HttpPost("import-update-excel")]
    public async Task<Result<List<MaterialResponse>>> ImportUpdateMaterialExcel([FromForm] ImportMaterialExcel input)
    {
        var command = new ImportUpdateAllMaterialExcelCommand
        {
            ExcelFile = input.ExcelFile,
            ImportDate = input.ImportDate
        };
        var result =await _mediator.Send(command);
        if (!result.Succeeded || result.Data == null)
        {
            return null;
        }
        return result;
    }
    [HttpGet("download-create-sample-excel")]
    public async Task<IActionResult> DownloadCreateSampleMaterialExcelFile()
    {
        var query = new DownloadCreateSampleMaterialExcelQuery();
        var result = await _mediator.Send(query);
        if (!result.Succeeded || result.Data == null)
        {
            return BadRequest(new { message = result.Message });
        }
        return result.Data;
    }
    [HttpPost("import-create-excel")]
    public async Task<Result<List<MaterialResponse>>> ImportCreateMaterialExcel([FromForm] ImportMaterialExcel input)
    {
        var command = new ImportCreateAllMaterialExcelCommand
        {
            ExcelFile = input.ExcelFile,
            ImportDate = input.ImportDate
        };
        var result =await _mediator.Send(command);
        if (!result.Succeeded || result.Data == null)
        {
            return null;
        }
        return result;
    }

}
