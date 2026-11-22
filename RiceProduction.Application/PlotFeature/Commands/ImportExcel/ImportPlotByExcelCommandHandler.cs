using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Request.PlotRequest;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.ImportExcel
{
    public class ImportPlotByExcelCommandHandler : IRequestHandler<ImportPlotByExcelCommand, Result<List<PlotResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IGenericExcel _genericExcel;
        private readonly IMediator _mediator;
        private readonly ILogger<ImportPlotByExcelCommandHandler> _logger;

        public ImportPlotByExcelCommandHandler(
            IApplicationDbContext context,
            IGenericExcel genericExcel,
            IMediator mediator,
            ILogger<ImportPlotByExcelCommandHandler> logger)
        {
            _context = context;
            _genericExcel = genericExcel;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result<List<PlotResponse>>> Handle(
            ImportPlotByExcelCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                var plotImportRows = await _genericExcel.ExcelToListT<PlotImportRow>(request.ExcelFile);
                return await PlotImportHelper.ExecuteImportAsync(
                    plotImportRows,
                    request.ClusterManagerId,
                    _context,
                    _mediator,
                    _logger,
                    PlotImportHelper.ExcelProfile,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing plots");
                return Result<List<PlotResponse>>.Failure(
                    $"Import failed: {ex.Message}");
            }
        }
    }
}
