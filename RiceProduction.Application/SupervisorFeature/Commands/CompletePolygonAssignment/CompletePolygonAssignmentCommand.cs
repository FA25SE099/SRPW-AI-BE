using MediatR;
using NetTopologySuite.Geometries;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.SupervisorFeature.Commands.CompletePolygonAssignment;

public class CompletePolygonAssignmentCommand : IRequest<Result<bool>>
{
    public Guid TaskId { get; set; }
    public Guid SupervisorId { get; set; }
    public string PolygonGeoJson { get; set; } = string.Empty; // GeoJSON format polygon
    public string? Notes { get; set; }
}

