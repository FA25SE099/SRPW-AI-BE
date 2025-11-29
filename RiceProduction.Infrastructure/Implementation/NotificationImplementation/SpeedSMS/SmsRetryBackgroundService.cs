//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using RiceProduction.Application.Common.Interfaces;
//using RiceProduction.Application.Common.Interfaces.External;

//namespace RiceProduction.Infrastructure.Implementation.NotificationImplementation.SpeedSMS;

///// <summary>
///// Background service that processes pending SMS retry attempts
///// </summary>
//public class SmsRetryBackgroundService : BackgroundService
//{
//    private readonly IServiceProvider _serviceProvider;
//    private readonly ILogger<SmsRetryBackgroundService> _logger;
//    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

//    public SmsRetryBackgroundService(
//        IServiceProvider serviceProvider,
//        ILogger<SmsRetryBackgroundService> logger)
//    {
//        _serviceProvider = serviceProvider;
//        _logger = logger;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.LogInformation("SMS Retry Background Service started");

//        // Wait a bit before starting to ensure application is fully initialized
//        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

//        while (!stoppingToken.IsCancellationRequested)
//        {
//            try
//            {
//                await ProcessPendingRetries(stoppingToken);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error processing SMS retries");
//            }

//            await Task.Delay(_checkInterval, stoppingToken);
//        }

//        _logger.LogInformation("SMS Retry Background Service stopped");
//    }

//    private async Task ProcessPendingRetries(CancellationToken cancellationToken)
//    {
//        using var scope = _serviceProvider.CreateScope();
//        var context = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
//        var retryService = scope.ServiceProvider.GetRequiredService<ISmsRetryService>();

//        var now = DateTime.UtcNow;

//        // Find notifications that are due for retry
//        var pendingRetries = context.Repository<Notification>().GetQueryable()
//            .Where(n => n.Status == "pending_retry" &&
//                        n.NextRetryAt.HasValue &&
//                        n.NextRetryAt.Value <= now &&
//                        n.RetryCount < n.MaxRetries)
//            .OrderBy(n => n.NextRetryAt)
//            .Take(10); // Process 10 at a time to avoid overwhelming the system
//        if (pendingRetries.Any())
//        {
//            _logger.LogInformation("Processing {Count} pending SMS retries", pendingRetries.Count());

//            foreach (var notification in pendingRetries)
//            {
//                if (cancellationToken.IsCancellationRequested)
//                    break;

//                try
//                {
//                    _logger.LogInformation(
//                        "Processing retry for notification {NotificationId}, attempt {RetryCount}/{MaxRetries}",
//                        notification.Id,
//                        notification.RetryCount + 1,
//                        notification.MaxRetries);

//                    await retryService.ProcessRetryAsync(notification.Id, cancellationToken);

//                    // Small delay between retries to avoid rate limiting
//                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, 
//                        "Error processing retry for notification {NotificationId}", 
//                        notification.Id);
//                }
//            }

//            _logger.LogInformation("Completed processing {Count} SMS retries", pendingRetries.Count());
//        }
//    }

//    public override async Task StopAsync(CancellationToken cancellationToken)
//    {
//        _logger.LogInformation("SMS Retry Background Service is stopping");
//        await base.StopAsync(cancellationToken);
//    }
//}
