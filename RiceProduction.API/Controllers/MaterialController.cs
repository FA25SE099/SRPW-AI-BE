using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiceProduction.API.Common;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.MaterialRequests;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.MaterialFeature.Commands.CreateMaterial;
using RiceProduction.Application.MaterialFeature.Commands.DeleteMaterial;
using RiceProduction.Application.MaterialFeature.Commands.ImportCreateAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Commands.ImportUpdateAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Commands.ImportUpsertMaterialExcel;
using RiceProduction.Application.MaterialFeature.Commands.UpdateMaterial;
using RiceProduction.Application.MaterialFeature.Queries.CalculateGroupMaterialCost;
using RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByArea;
using RiceProduction.Application.MaterialFeature.Queries.CalculateMaterialsCostByPlotId;
using RiceProduction.Application.MaterialFeature.Queries.CalculatePrice;
using RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanMaterialCost;
using RiceProduction.Application.MaterialFeature.Queries.CalculateStandardPlanProfitAnalysis;
using RiceProduction.Application.MaterialFeature.Queries.DownloadAllMaterialExcel;
using RiceProduction.Application.MaterialFeature.Queries.DownloadCreateSampleMaterialExcel;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;
using RiceProduction.Application.MaterialFeature.Queries.GetMaterialById;

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
    public async Task<ActionResult<PagedResult<List<MaterialResponseForList>>>> AllMaterialPaging([FromForm] MaterialListRequest request)
    {
        var query = new GetAllMaterialByTypeQuery
        {
            CurrentPage = request.CurrentPage,
            PageSize = request.PageSize,
            MaterialType = request.Type,
            PriceDateTime = request.PriceDateTime
        };
        return this.ToActionResult(await _mediator.Send(query));
    }

    [HttpGet("by-id")]
    public async Task<IActionResult> GetMaterialById([FromQuery] Guid id)
    {
        var query = new GetMaterialByIdQuery { MaterialId = id };
        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpPost("download-excel")]
    public async Task<IActionResult> DownloadMaterialExcel([FromBody] DateTime request)
    {
        var query = new DownloadAllMaterialExcelQuery { InputDate = request };
        return ToFileActionResult(await _mediator.Send(query));
    }

    [HttpPost("import-update-excel")]
    public async Task<Result<List<MaterialResponse>>> ImportUpdateMaterialExcel([FromForm] ImportMaterialExcel input)
    {
        var command = new ImportUpdateAllMaterialExcelCommand
        {
            ExcelFile = input.ExcelFile,
            ImportDate = input.ImportDate
        };
        var result = await _mediator.Send(command);
        return result.Succeeded && result.Data != null ? result : null!;
    }

    [HttpGet("download-create-sample-excel")]
    public async Task<IActionResult> DownloadCreateSampleMaterialExcelFile()
    {
        var query = new DownloadCreateSampleMaterialExcelQuery();
        return ToFileActionResult(await _mediator.Send(query));
    }

    [HttpPost("import-create-excel")]
    public async Task<Result<List<MaterialResponse>>> ImportCreateMaterialExcel([FromForm] ImportMaterialExcel input)
    {
        var command = new ImportCreateAllMaterialExcelCommand
        {
            ExcelFile = input.ExcelFile,
            ImportDate = input.ImportDate
        };
        var result = await _mediator.Send(command);
        return result.Succeeded && result.Data != null ? result : null!;
    }

    [HttpPost("import-upsert-excel")]
    public async Task<IActionResult> ImportUpsertMaterialExcel([FromForm] ImportMaterialExcel input)
    {
        var command = new ImportUpsertMaterialExcelCommand
        {
            ExcelFile = input.ExcelFile,
            ImportDate = input.ImportDate
        };
        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialCommand command)
    {
        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMaterial(Guid id, [FromBody] UpdateMaterialCommand command)
    {
        if (id != command.MaterialId)
        {
            return BadRequest(new { message = "Route ID does not match command ID" });
        }
        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMaterial(Guid id)
    {
        var command = new DeleteMaterialCommand { MaterialId = id };
        return this.ToIActionResult(await _mediator.Send(command));
    }

    [HttpGet("calculate-price")]
    public async Task<IActionResult> CalculatePrice([FromQuery] CalculateMaterialPriceQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpPost("calculate-group-material-cost")]
    public async Task<IActionResult> CalculateGroupMaterialCost([FromBody] CalculateGroupMaterialCostQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpPost("calculate-materials-cost-by-area")]
    public async Task<IActionResult> CalculateMaterialsCostByArea([FromBody] CalculateMaterialsCostByAreaQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    [HttpPost("calculate-materials-cost-by-plot-id")]
    public async Task<IActionResult> CalculateMaterialsCostByPlotId([FromBody] CalculateMaterialsCostByPlotIdQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Calculate material costs from a Standard Plan
    /// </summary>
    /// <remarks>
    /// Calculates the total material costs based on a Standard Plan.
    /// Either PlotId or Area must be provided.
    /// If PlotId is provided, the actual area from the plot will be used.
    /// If Area is provided, that value will be used for calculation.
    /// The materials and their quantities are retrieved from the specified Standard Plan.
    /// </remarks>
    [HttpPost("calculate-standard-plan-material-cost")]
    public async Task<IActionResult> CalculateStandardPlanMaterialCost([FromBody] CalculateStandardPlanMaterialCostQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    /// <summary>
    /// Calculate profit analysis from a Standard Plan
    /// </summary>
    /// <remarks>
    /// Performs a comprehensive profit analysis based on a Standard Plan.
    /// Calculates:
    /// - Revenue per hectare (PricePerKgRice * ExpectedYieldPerHa)
    /// - Material cost per hectare (from standard plan materials)
    /// - Total cost per hectare (material cost + other service costs)
    /// - Profit per hectare (revenue - total cost)
    /// - All values scaled to the specified area
    /// 
    /// Either PlotId or Area must be provided.
    /// If PlotId is provided, the actual area from the plot will be used.
    /// </remarks>
    [HttpPost("calculate-standard-plan-profit-analysis")]
    public async Task<IActionResult> CalculateStandardPlanProfitAnalysis([FromBody] CalculateStandardPlanProfitAnalysisQuery query)
    {
        return this.ToIActionResult(await _mediator.Send(query));
    }

    private IActionResult ToFileActionResult<T>(Result<T> result) where T : IActionResult =>
        result.Succeeded && result.Data != null
            ? result.Data
            : BadRequest(new { message = result.Message });
}
