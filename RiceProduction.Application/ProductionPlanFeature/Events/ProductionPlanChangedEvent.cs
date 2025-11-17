using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.ProductionPlanFeature.Events
{
    public class ProductionPlanChangedEvent : INotification
        {
            public Guid ProductionPlanId { get; set; }
            public ChangeType ChangeType { get; set; }

            public ProductionPlanChangedEvent(Guid productionPlanId, ChangeType changeType)
            {
                ProductionPlanId = productionPlanId;
                ChangeType = changeType;
            }
        }
}