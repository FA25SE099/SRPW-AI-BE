using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Helpers;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Commands.ImportFromData;

public class ImportPlotsFromDataCommandHandler
    : IRequestHandler<ImportPlotsFromDataCommand, Result<List<PlotResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<ImportPlotsFromDataCommandHandler> _logger;

    public ImportPlotsFromDataCommandHandler(
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<ImportPlotsFromDataCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<List<PlotResponse>>> Handle(
        ImportPlotsFromDataCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PlotImportHelper.ExecuteImportAsync(
                request.PlotRows,
                request.ClusterManagerId,
                _unitOfWork,
                _mediator,
                _logger,
                PlotImportHelper.DataImportProfile,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi nhập thửa đất từ dữ liệu");
            return Result<List<PlotResponse>>.Failure(
                $"Nhập thất bại: {ex.Message}");
        }
    }
}
