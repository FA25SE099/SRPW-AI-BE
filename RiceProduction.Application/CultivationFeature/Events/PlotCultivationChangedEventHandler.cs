using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.CultivationFeature.Events
{
    public class PlotCultivationChangedEventHandler : INotificationHandler<PlotCultivationChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<PlotCultivationChangedEventHandler> _logger;

        public PlotCultivationChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<PlotCultivationChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(PlotCultivationChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for PlotCultivation {PlotCultivationId} due to {ChangeType}",
                notification.PlotCultivationId, notification.ChangeType);

            _cacheInvalidator.InvalidateCachesByPattern($"FarmerCultivationTasks:Farmer:{notification.FarmerId}*");
            _cacheInvalidator.InvalidateCachesByPattern($"SeasonEconomicOverview:Season:{notification.SeasonId}*");
            _cacheInvalidator.InvalidateCachesByPattern($"SeasonCostAnalysis:Season:{notification.SeasonId}*");
            _cacheInvalidator.InvalidateCachesByPattern($"SeasonYieldAnalysis:Season:{notification.SeasonId}*");
            _cacheInvalidator.InvalidateCachesByPattern($"SeasonComparison:*");

            _logger.LogInformation(
                "Successfully invalidated plot cultivation caches for {PlotCultivationId}",
                notification.PlotCultivationId);

            return Task.CompletedTask;
        }
    }
}

