using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.MaterialFeature.Events
{
    public class MaterialChangedEventHandler : INotificationHandler<MaterialChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<MaterialChangedEventHandler> _logger;

        public MaterialChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<MaterialChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(MaterialChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for Material {MaterialId} due to {ChangeType}",
                notification.MaterialId, notification.ChangeType);

            _cacheInvalidator.InvalidateCachesByPattern("Materials:*");

            _logger.LogInformation(
                "Successfully invalidated material caches for {MaterialId}",
                notification.MaterialId);

            return Task.CompletedTask;
        }
    }
}

