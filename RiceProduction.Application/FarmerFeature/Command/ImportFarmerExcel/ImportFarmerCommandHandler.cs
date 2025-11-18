using MediatR; // Cần thêm using này
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.SmsFeature.Event;
using RiceProduction.Application.FarmerFeature.Events;

namespace RiceProduction.Application.FarmerFeature.Command.ImportFarmer
{
    public class ImportFarmerCommandHandler : IRequestHandler<ImportFarmerCommand, ImportFarmerResult>
    {
        private readonly IFarmerExcel _farmerExcel;
        private readonly ILogger<ImportFarmerCommandHandler> _logger;
        private readonly IMediator _mediator;
        public ImportFarmerCommandHandler(IFarmerExcel farmerExcel, ILogger<ImportFarmerCommandHandler> logger, IMediator mediator)
        {
            _farmerExcel = farmerExcel;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<ImportFarmerResult> Handle(ImportFarmerCommand request, CancellationToken cancellationToken)
        {
            if (request.File == null || request.File.Length == 0)
            {
                _logger.LogError("Import failed: File is null or empty.");

                var errorResult = new ImportFarmerResult
                {
                    TotalRows = 0,
                    FailureCount = 0,
                    SuccessCount = 0,
                    Errors = { new ImportError { ErrorMessage = "File không được để trống." } }
                };

                return await Task.FromResult(errorResult);
            }

            _logger.LogInformation("Starting farmer import for file: {FileName}", request.File.FileName);

            var result = await _farmerExcel.ImportFarmerFromExcelAsync(
                request.File,
                request.ClusterManagerId,
                cancellationToken
            );

            _logger.LogInformation(
                "Farmer import finished. Success: {Success}, Failed: {Failed}",
                result.SuccessCount,
                result.FailureCount
            );
            
            // Note: Plots are now imported separately using the plot import template
            // No need to publish event for polygon assignment here
            
            return result;
        }
    }
}