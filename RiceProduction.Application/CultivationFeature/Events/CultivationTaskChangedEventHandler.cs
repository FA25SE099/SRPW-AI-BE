using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.CultivationFeature.Events
{
    public class CultivationTaskChangedEventHandler : INotificationHandler<CultivationTaskChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<CultivationTaskChangedEventHandler> _logger;

        public CultivationTaskChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<CultivationTaskChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(CultivationTaskChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for CultivationTask {CultivationTaskId} due to {ChangeType}",
                notification.CultivationTaskId, notification.ChangeType);

            _cacheInvalidator.InvalidateCachesByPattern($"FarmerCultivationTasks:Farmer:{notification.FarmerId}*");
            _cacheInvalidator.InvalidateCachesByPattern($"*Season:{notification.SeasonId}*");

            _logger.LogInformation(
                "Successfully invalidated cultivation task caches for {CultivationTaskId}",
                notification.CultivationTaskId);

            return Task.CompletedTask;
        }
    }
}

