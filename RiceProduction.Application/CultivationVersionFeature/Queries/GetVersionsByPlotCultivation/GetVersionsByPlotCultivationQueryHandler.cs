using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.CultivationVersionFeature.Queries.GetVersionsByPlotCultivation;

public class GetVersionsByPlotCultivationQueryHandler : IRequestHandler<GetVersionsByPlotCultivationQuery, Result<List<CultivationVersionResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetVersionsByPlotCultivationQueryHandler> _logger;

    public GetVersionsByPlotCultivationQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetVersionsByPlotCultivationQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<CultivationVersionResponse>>> Handle(
        GetVersionsByPlotCultivationQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify PlotCultivation exists
            var plotCultivation = await _unitOfWork.Repository<PlotCultivation>()
                .FindAsync(pc => pc.Id == request.PlotCultivationId);

            if (plotCultivation == null)
            {
                return Result<List<CultivationVersionResponse>>.Failure(
                    "Plot Cultivation not found.",
                    "NotFound");
            }

            // Get all versions for this plot cultivation
            var versions = await _unitOfWork.Repository<CultivationVersion>().ListAsync(
                filter: v => v.PlotCultivationId == request.PlotCultivationId,
                orderBy: q => q.OrderByDescending(v => v.VersionOrder)
            );

            // Get task counts for each version
            var versionIds = versions.Select(v => v.Id).ToList();
            var tasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                filter: ct => ct.PlotCultivationId == request.PlotCultivationId 
                    && ct.VersionId.HasValue 
                    && versionIds.Contains(ct.VersionId.Value)
            );

            var taskCountByVersion = tasks
                .GroupBy(t => t.VersionId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

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
