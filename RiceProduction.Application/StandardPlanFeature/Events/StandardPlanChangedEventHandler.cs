using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;

namespace RiceProduction.Application.StandardPlanFeature.Events;

public class StandardPlanChangedEventHandler : INotificationHandler<StandardPlanChangedEvent>
{
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly ILogger<StandardPlanChangedEventHandler> _logger;

    public StandardPlanChangedEventHandler(
        ICacheInvalidator cacheInvalidator,
        ILogger<StandardPlanChangedEventHandler> logger)
    {
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
    }

    public Task Handle(StandardPlanChangedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Invalidating caches for StandardPlan {StandardPlanId} due to {ChangeType}",
            notification.StandardPlanId, notification.ChangeType);

        var cachesToInvalidate = new List<string>
        {
            "StandardPlans",
            $"StandardPlanDetail:{notification.StandardPlanId}",
            $"ReviewStandardPlan:{notification.StandardPlanId}"
        };

        _cacheInvalidator.InvalidateCaches(cachesToInvalidate.ToArray());

        _logger.LogInformation(
            "Successfully invalidated {Count} cache entries for StandardPlan {StandardPlanId}",
            cachesToInvalidate.Count, notification.StandardPlanId);

        return Task.CompletedTask;
    }
}
