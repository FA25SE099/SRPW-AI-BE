using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.SeasonFeature.Events
{
    public class SeasonChangedEventHandler : INotificationHandler<SeasonChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<SeasonChangedEventHandler> _logger;

        public SeasonChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<SeasonChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(SeasonChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for Season {SeasonId} due to {ChangeType}",
                notification.SeasonId, notification.ChangeType);

            var cachesToInvalidate = new List<string>();
            
            cachesToInvalidate.Add($"Seasons:*");
            cachesToInvalidate.Add($"SeasonEconomicOverview:Season:{notification.SeasonId}*");
            cachesToInvalidate.Add($"SeasonCostAnalysis:Season:{notification.SeasonId}*");
            cachesToInvalidate.Add($"SeasonYieldAnalysis:Season:{notification.SeasonId}*");
            cachesToInvalidate.Add($"SeasonComparison:*");

            foreach (var pattern in cachesToInvalidate)
            {
                _cacheInvalidator.InvalidateCachesByPattern(pattern);
            }

            _logger.LogInformation(
                "Successfully invalidated season caches for {SeasonId}",
                notification.SeasonId);

            return Task.CompletedTask;
        }
    }
}

