using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationFeature.Events
{
    public class CultivationTaskChangedEvent : INotification
    {
        public Guid CultivationTaskId { get; set; }
        public Guid FarmerId { get; set; }
        public Guid SeasonId { get; set; }
        public ChangeType ChangeType { get; set; }
        
        public CultivationTaskChangedEvent(Guid cultivationTaskId, Guid farmerId, Guid seasonId, ChangeType changeType)
        {
            CultivationTaskId = cultivationTaskId;
            FarmerId = farmerId;
            SeasonId = seasonId;
            ChangeType = changeType;
        }
    }
}

