using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SeasonFeature.Events
{
    public class SeasonChangedEvent : INotification
    {
        public Guid SeasonId { get; set; }
        public ChangeType ChangeType { get; set; }
        
        public SeasonChangedEvent(Guid seasonId, ChangeType changeType)
        {
            SeasonId = seasonId;
            ChangeType = changeType;
        }
    }
}

