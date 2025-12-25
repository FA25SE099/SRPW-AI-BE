using MediatR; 
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Events;
using static RiceProduction.Application.FarmerFeature.Events.SendEmailEvent.FarmerImportedEvent;

namespace RiceProduction.Application.FarmerFeature.Command.ImportFarmer
{
    public class ImportFarmerCommandHandler : IRequestHandler<ImportFarmerCommand, ImportFarmerResult>
    {
        private readonly IFarmerExcel _farmerExcel;
        private readonly ILogger<ImportFarmerCommandHandler> _logger;
        private readonly IMediator _mediator;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IEmailJobService _emailJobService;
        public ImportFarmerCommandHandler(
            IFarmerExcel farmerExcel, 
            ILogger<ImportFarmerCommandHandler> logger, 
            IMediator mediator, IBackgroundTaskQueue taskQueue, IEmailJobService emailJobService)
        {
            _farmerExcel = farmerExcel;
            _logger = logger;
            _mediator = mediator;
            _taskQueue = taskQueue;
            _emailJobService = emailJobService;
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

            // Publish event for async email sending (non-blocking)
            if (result.SuccessCount > 0 && result.ImportedFarmers.Any())
            {
                await _mediator.Publish(new FarmerWelcomeImportedEvent
                {
                    ImportedFarmers = result.ImportedFarmers.DistinctBy(f => f.Email).ToList(), 
                    ImportedAt = DateTime.UtcNow
                }, cancellationToken);
            }

            // Note: Plots are now imported separately using the plot import template
            // No need to publish event for polygon assignment here

            return result;
        }
    }
}