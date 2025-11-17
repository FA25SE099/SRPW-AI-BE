using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.ProductionPlanFeature.Events
{
    public class ProductionPlanChangedEventHandler : INotificationHandler<ProductionPlanChangedEvent>
    {
        private readonly ICacheInvalidator _cacheInvalidator;
        private readonly ILogger<ProductionPlanChangedEventHandler> _logger;

        public ProductionPlanChangedEventHandler(
            ICacheInvalidator cacheInvalidator,
            ILogger<ProductionPlanChangedEventHandler> logger)
        {
            _cacheInvalidator = cacheInvalidator;
            _logger = logger;
        }

        public Task Handle(ProductionPlanChangedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Invalidating caches for Production Plan {ProductionPlanId} due to {ChangeType}",
                notification.ProductionPlanId, notification.ChangeType);

            var cachesToInvalidate = new List<string>();

            cachesToInvalidate.Add($"Plots:*");
            cachesToInvalidate.Add($"PlotEconomicOverview:Plot:{notification.ProductionPlanId}*");
            cachesToInvalidate.Add($"PlotCostAnalysis:Plot:{notification.ProductionPlanId}*");
            cachesToInvalidate.Add($"PlotYieldAnalysis:Plot:{notification.ProductionPlanId}*");
            cachesToInvalidate.Add($"PlotComparison:*");

            foreach (var pattern in cachesToInvalidate)
            {
                _cacheInvalidator.InvalidateCachesByPattern(pattern);
            }

            _logger.LogInformation(
                "Successfully invalidated production plan caches for {ProductionPlanId}",
                notification.ProductionPlanId);

            return Task.CompletedTask;
        }
    }
}

