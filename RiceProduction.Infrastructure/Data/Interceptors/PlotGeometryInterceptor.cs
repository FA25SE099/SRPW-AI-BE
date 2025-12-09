using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities; // Adjust namespace as needed

namespace RiceProduction.Infrastructure.Data.Interceptors;

public class PlotGeometryInterceptor : SaveChangesInterceptor
{
    private readonly GeometryFactory _geometryFactory;

    public PlotGeometryInterceptor()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdatePlotCentroids(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdatePlotCentroids(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdatePlotCentroids(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker
            .Entries<Plot>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            // Only recalculate if Boundary was changed or it's a new entity
            if (entry.State == EntityState.Added ||
                entry.Property(p => p.Boundary).IsModified)
            {
                var polygon = entry.Entity.Boundary;

                if (polygon != null && !polygon.IsEmpty && polygon.IsValid)
                {
                    var centroid = polygon.Centroid;

                    // Ensure SRID is preserved (important for PostGIS)
                    centroid.SRID = polygon.SRID;

                    entry.Entity.Coordinate = _geometryFactory.CreatePoint(centroid.Coordinate);
                    entry.Entity.Coordinate.SRID = polygon.SRID;
                }
                else
                {
                    // Optional: set to null or throw if invalid
                    entry.Entity.Coordinate = null;
                }
            }
        }
    }
}