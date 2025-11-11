using MediatR;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationFeature.Events
{
    public class PlotCultivationChangedEvent : INotification
    {
        public Guid PlotCultivationId { get; set; }
        public Guid SeasonId { get; set; }
        public Guid FarmerId { get; set; }
        public ChangeType ChangeType { get; set; }
        
        public PlotCultivationChangedEvent(Guid plotCultivationId, Guid seasonId, Guid farmerId, ChangeType changeType)
        {
            PlotCultivationId = plotCultivationId;
            SeasonId = seasonId;
            FarmerId = farmerId;
            ChangeType = changeType;
        }
    }
}

