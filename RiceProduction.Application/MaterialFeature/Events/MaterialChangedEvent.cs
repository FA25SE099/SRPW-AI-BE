using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.MaterialFeature.Events
{
    public class MaterialChangedEvent : INotification
    {
        public Guid? MaterialId { get; set; }
        public ChangeType ChangeType { get; set; }
        
        public MaterialChangedEvent(Guid? materialId, ChangeType changeType)
        {
            MaterialId = materialId;
            ChangeType = changeType;
        }
    }
}

