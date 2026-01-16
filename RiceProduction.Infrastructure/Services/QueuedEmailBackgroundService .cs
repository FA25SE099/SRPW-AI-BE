using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Infrastructure.Services
{
    public class QueuedEmailBackgroundService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<QueuedEmailBackgroundService> _logger;

        public QueuedEmailBackgroundService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedEmailBackgroundService> logger)
        {
            _taskQueue = taskQueue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Email Background Service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    await workItem(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing email task");
                }
            }

            _logger.LogInformation("Queued Email Background Service is stopping");
        }
    }
}