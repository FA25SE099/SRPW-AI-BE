using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.RiceVarietyFeature.Events
{
    public class RiceVarietyChangedEvent : INotification
    {
        public Guid? RiceVarietyId { get; set; }
        public ChangeType ChangeType { get; set; }
        
        public RiceVarietyChangedEvent(Guid? riceVarietyId, ChangeType changeType)
        {
            RiceVarietyId = riceVarietyId;
            ChangeType = changeType;
        }
    }
}

