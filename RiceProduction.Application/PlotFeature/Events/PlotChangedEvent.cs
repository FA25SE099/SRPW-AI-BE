using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.PlotFeature.Events
{
    public class PlotChangedEvent : INotification
        {
            public Guid PlotId { get; set; }
            public ChangeType ChangeType { get; set; }
            
            public PlotChangedEvent(Guid plotId, ChangeType changeType)
            {
                PlotId = plotId;
                ChangeType = changeType;
            }
        }
}