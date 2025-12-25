using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportById;

public class GetReportByIdQueryHandler : IRequestHandler<GetReportByIdQuery, Result<ReportItemResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetReportByIdQueryHandler> _logger;

    public GetReportByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetReportByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ReportItemResponse>> Handle(
        GetReportByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.Farmer)
                .Include(r => r.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                        .ThenInclude(p => p.GroupPlots)
                            .ThenInclude(gp => gp.Group)
                                .ThenInclude(g => g.Cluster)
                .Include(r => r.Reporter)
                .Include(r => r.Resolver)
                .Include(r => r.Group)
                    .ThenInclude(g => g.Cluster)
                .Include(r => r.Cluster)
                     .Include(r => r.AffectedTask)
                    .ThenInclude(t => t.ProductionPlanTask)
                .Include(r => r.AffectedTask)
                    .ThenInclude(t => t.Version)
                .FirstOrDefaultAsync(r => r.Id == request.ReportId, cancellationToken);

            if (report == null)
            {
                return Result<ReportItemResponse>.Failure($"Report with ID {request.ReportId} not found.", "NotFound");
            }

            var response = new ReportItemResponse
            {
                Id = report.Id,
                PlotId = report.PlotCultivation?.PlotId,
                PlotName = report.PlotCultivation?.Plot != null
                    ? $"{report.PlotCultivation.Plot.SoThua}/{report.PlotCultivation.Plot.SoTo}"
                    : null,
                PlotArea = report.PlotCultivation?.Plot?.Area,
                GroupId = report.PlotCultivation.Plot.GroupPlots.FirstOrDefault().Group.Id,
                GroupName = report.PlotCultivation.Plot.GroupPlots.FirstOrDefault().Group.GroupName,
                CultivationPlanId = report.PlotCultivationId,
                CultivationPlanName = report.PlotCultivation != null
                    ? $"Plan {report.PlotCultivation.PlantingDate:yyyy-MM-dd}"
                    : null,
                ReportType = report.AlertType,
                Severity = report.Severity.ToString(),
                Title = report.Title,
                Description = report.Description,
                ReportedBy = report.Reporter?.FullName ?? "Unknown",
                ReportedByRole = GetReportedByRole(report.Source),
                ReportedAt = report.CreatedAt.DateTime,
                Status = report.Status.ToString(),
                Images = report.ImageUrls,
                Coordinates = report.Coordinates,
                ResolvedBy = report.Resolver?.FullName,
                ResolvedAt = report.ResolvedAt,
                ResolutionNotes = report.ResolutionNotes,
                FarmerName = report.PlotCultivation?.Plot?.Farmer?.FullName,
                ClusterName = report.PlotCultivation?.Plot?.GroupPlots?.FirstOrDefault()?.Group?.Cluster?.ClusterName
                    ?? report.Group?.Cluster?.ClusterName
                    ?? report.Cluster?.ClusterName,
                AffectedCultivationTaskId = report.AffectedCultivationTaskId,
                AffectedTaskName = report.AffectedTask?.CultivationTaskName
                    ?? report.AffectedTask?.ProductionPlanTask?.TaskName,
                AffectedTaskType = report.AffectedTask?.TaskType?.ToString(),
                AffectedTaskVersionName = report.AffectedTask?.Version?.VersionName,
                AffectedTaskVersionId = report.AffectedTask?.VersionId
            };

            return Result<ReportItemResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report {ReportId}", request.ReportId);
            return Result<ReportItemResponse>.Failure("An error occurred while retrieving the report.");
        }
    }

    private static string? GetReportedByRole(AlertSource source)
    {
        return source switch
        {
            AlertSource.FarmerReport => "Farmer",
            AlertSource.SupervisorInspection => "Supervisor",
            _ => null
        };
    }
}

