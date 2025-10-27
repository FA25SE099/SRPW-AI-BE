using MediatR;

namespace RiceProduction.Application.StandardPlanFeature.Events;

public class StandardPlanChangedEvent : INotification
{
    public Guid StandardPlanId { get; set; }
    public ChangeType ChangeType { get; set; }
    
    public StandardPlanChangedEvent(Guid standardPlanId, ChangeType changeType)
    {
        StandardPlanId = standardPlanId;
        ChangeType = changeType;
    }
}

public enum ChangeType
{
    Created,
    Updated,
    Deleted,
    StatusChanged
}
