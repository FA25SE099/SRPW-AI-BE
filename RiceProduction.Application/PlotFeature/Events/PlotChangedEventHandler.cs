using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.PlotFeature.Events
{
    public class PlotChangedEventHandler : INotificationHandler<PlotChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<PlotChangedEventHandler> _logger;

        public PlotChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<PlotChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(PlotChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for Plot {PlotId} due to {ChangeType}",
                notification.PlotId, notification.ChangeType);

            var cachesToInvalidate = new List<string>();

            cachesToInvalidate.Add($"Plots:*");
            cachesToInvalidate.Add($"PlotEconomicOverview:Plot:{notification.PlotId}*");
            cachesToInvalidate.Add($"PlotCostAnalysis:Plot:{notification.PlotId}*");
            cachesToInvalidate.Add($"PlotYieldAnalysis:Plot:{notification.PlotId}*");
            cachesToInvalidate.Add($"PlotComparison:*");

            foreach (var pattern in cachesToInvalidate)
            {
                _cacheInvalidator.InvalidateCachesByPattern(pattern);
            }

            _logger.LogInformation(
                "Successfully invalidated plot caches for {PlotId}",
                notification.PlotId);

            return Task.CompletedTask;
        }
    }
}

