using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.RiceVarietyFeature.Events
{
    public class RiceVarietyChangedEventHandler : INotificationHandler<RiceVarietyChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<RiceVarietyChangedEventHandler> _logger;

        public RiceVarietyChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<RiceVarietyChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(RiceVarietyChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for RiceVariety {RiceVarietyId} due to {ChangeType}",
                notification.RiceVarietyId, notification.ChangeType);

            _cacheInvalidator.InvalidateCachesByPattern("RiceVarieties:*");

            _logger.LogInformation(
                "Successfully invalidated rice variety caches for {RiceVarietyId}",
                notification.RiceVarietyId);

            return Task.CompletedTask;
        }
    }
}

