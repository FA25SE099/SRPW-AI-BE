using MediatR; // Cần thêm using này
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
            //if (result.SuccessCount > 0 && result.ImportedFarmers.Any())
            //{
            //    await _mediator.Publish(new FarmersImportedEvent
            //    {
            //        ImportedFarmers = result.ImportedFarmers,
            //        ImportedAt = DateTime.UtcNow
            //    }, cancellationToken);

            //    var emailCount = result.ImportedFarmers.Count(f => !string.IsNullOrWhiteSpace(f.Email));
            //    _logger.LogInformation("Published farmer import event for {EmailCount} email notifications", emailCount);
            //}

            // Note: Plots are now imported separately using the plot import template
            // No need to publish event for polygon assignment here
            if (result.SuccessCount > 0 && result.ImportedFarmers.Any())
            {
                var emailCount = result.ImportedFarmers.Count(f => !string.IsNullOrWhiteSpace(f.Email));

                if (emailCount > 0)
                {
                    
                    var importedFarmersInfo = result.ImportedFarmers
                        .Where(f => !string.IsNullOrWhiteSpace(f.Email))
                        .Select(f => new ImportedFarmerInfo
                        {
                            FarmerId = Guid.NewGuid(), 
                            FullName = f.FullName,
                            PhoneNumber = f.PhoneNumber,
                            Email = f.Email,
                            Password = f.TempPassword ?? "Farmer@123",
                            FarmCode = f.FarmCode
                        }).ToList();

                    
                    _taskQueue.QueueEmailTask(async ct =>
                    {
                        try
                        {
                            _logger.LogInformation("Processing queued bulk email for {Count} farmers", emailCount);

                            await _emailJobService.SendBulkFarmerAccountEmailsAsync(importedFarmersInfo, ct);

                            _logger.LogInformation("Successfully completed bulk email task");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send bulk emails for imported farmers");
                        }
                    });

                    _logger.LogInformation("Queued bulk email task for {EmailCount} farmers", emailCount);
                }
            }
            return result;
        }
    }
}