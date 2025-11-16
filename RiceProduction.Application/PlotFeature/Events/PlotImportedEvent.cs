using MediatR;
using RiceProduction.Application.Common.Models.Response.PlotResponse;

namespace RiceProduction.Application.PlotFeature.Events;

public class PlotImportedEvent : INotification
{
    public List<PlotResponse> ImportedPlots { get; set; } = new();
    public Guid? ClusterManagerId { get; set; }
    public DateTime ImportedAt { get; set; }
    public int TotalPlotsImported { get; set; }
}

