using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.CultivationVersionFeature.Queries.GetVersionsByPlotCultivation;

public class GetVersionsByPlotCultivationQueryHandler : IRequestHandler<GetVersionsByPlotCultivationQuery, Result<List<CultivationVersionResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetVersionsByPlotCultivationQueryHandler> _logger;

    public GetVersionsByPlotCultivationQueryHandler(
        IApplicationDbContext context,
        ILogger<GetVersionsByPlotCultivationQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<CultivationVersionResponse>>> Handle(
        GetVersionsByPlotCultivationQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var plotCultivationExists = await _context.PlotCultivations
                .AsNoTracking()
                .AnyAsync(pc => pc.Id == request.PlotCultivationId, cancellationToken);

            if (!plotCultivationExists)
            {
                return Result<List<CultivationVersionResponse>>.Failure(
                    "Plot Cultivation not found.",
                    "NotFound");
            }

            var versions = await _context.CultivationVersions
                .AsNoTracking()
                .Where(v => v.PlotCultivationId == request.PlotCultivationId)
                .OrderByDescending(v => v.VersionOrder)
                .ToListAsync(cancellationToken);

            var versionIds = versions.Select(v => v.Id).ToList();
            var taskCountByVersion = await _context.CultivationTasks
                .AsNoTracking()
                .Where(ct => ct.PlotCultivationId == request.PlotCultivationId
                    && ct.VersionId.HasValue
                    && versionIds.Contains(ct.VersionId.Value))
                .GroupBy(ct => ct.VersionId!.Value)
                .Select(g => new { VersionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VersionId, x => x.Count, cancellationToken);

            // Map to response
            var response = versions.Select(v => new CultivationVersionResponse
            {
                Id = v.Id,
                PlotCultivationId = v.PlotCultivationId,
                VersionName = v.VersionName,
                VersionOrder = v.VersionOrder,
                IsActive = v.IsActive,
                Reason = v.Reason,
                ActivatedAt = v.ActivatedAt,
                CreatedAt = v.CreatedAt.UtcDateTime,
                TaskCount = taskCountByVersion.GetValueOrDefault(v.Id, 0)
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} versions for PlotCultivation {PlotCultivationId}",
                response.Count,
                request.PlotCultivationId);

            return Result<List<CultivationVersionResponse>>.Success(
                response,
                $"Successfully retrieved {response.Count} version(s).");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving versions for PlotCultivation {PlotCultivationId}",
                request.PlotCultivationId);
            return Result<List<CultivationVersionResponse>>.Failure(
                "An error occurred while retrieving versions.",
                "GetVersionsFailed");
        }
    }
}
