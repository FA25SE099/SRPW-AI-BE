using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportsBySupervisor;

public class GetReportsBySupervisorQueryHandler : IRequestHandler<GetReportsBySupervisorQuery, PagedResult<List<ReportItemResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetReportsBySupervisorQueryHandler> _logger;

    public GetReportsBySupervisorQueryHandler(
        IApplicationDbContext context,
        ILogger<GetReportsBySupervisorQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<List<ReportItemResponse>>> Handle(
        GetReportsBySupervisorQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var supervisorExists = await _context.Supervisors
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SupervisorId, cancellationToken);
            if (!supervisorExists)
            {
                return PagedResult<List<ReportItemResponse>>.Failure(
                    "Supervisor not found.",
                    "NotFound");
            }

            var groupIds = await _context.Groups
                .AsNoTracking()
                .Where(g => g.SupervisorId == request.SupervisorId)
                .Select(g => g.Id)
                .ToListAsync(cancellationToken);

            if (groupIds.Count == 0)
            {
                return PagedResult<List<ReportItemResponse>>.Success(
                    new List<ReportItemResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No groups assigned to this supervisor.");
            }

            var supervisedPlotIds = await _context.GroupPlots
                .AsNoTracking()
                .Where(gp => groupIds.Contains(gp.GroupId))
                .Select(gp => gp.PlotId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (supervisedPlotIds.Count == 0)
            {
                return PagedResult<List<ReportItemResponse>>.Success(
                    new List<ReportItemResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No plots found in supervised groups.");
            }

            IQueryable<EmergencyReport> query = _context.EmergencyReports
                .AsNoTracking()
                .Where(r => r.PlotCultivation != null && supervisedPlotIds.Contains(r.PlotCultivation.PlotId))
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
                    .ThenInclude(t => t.Version);

            query = ApplyFilters(query, request).OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync(cancellationToken);

            var pagedReports = await query
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var responseData = pagedReports.Select(MapToReportItemResponse).ToList();

            _logger.LogInformation(
                "Retrieved {Count} reports for supervisor {SupervisorId} (from {PlotCount} plots in {GroupCount} groups)",
                responseData.Count,
                request.SupervisorId,
                supervisedPlotIds.Count,
                groupIds.Count);

            return PagedResult<List<ReportItemResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved reports for supervised farmers.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving reports for supervisor {SupervisorId}",
                request.SupervisorId);
            return PagedResult<List<ReportItemResponse>>.Failure(
                "An error occurred while retrieving reports.",
                "GetReportsFailed");
        }
    }

    private static IQueryable<EmergencyReport> ApplyFilters(
        IQueryable<EmergencyReport> query,
        GetReportsBySupervisorQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<AlertStatus>(request.Status, true, out var statusEnum))
        {
            query = query.Where(r => r.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity) &&
            Enum.TryParse<AlertSeverity>(request.Severity, true, out var severityEnum))
        {
            query = query.Where(r => r.Severity == severityEnum);
        }

        if (!string.IsNullOrWhiteSpace(request.ReportType))
        {
            var reportType = request.ReportType.Trim().ToLower();
            query = query.Where(r => r.AlertType.ToLower() == reportType);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower().Trim();
            query = query.Where(r =>
                r.Title.ToLower().Contains(searchTerm) ||
                r.Description.ToLower().Contains(searchTerm) ||
                (r.PlotCultivation != null && r.PlotCultivation.Plot != null && r.PlotCultivation.Plot.Farmer != null &&
                    r.PlotCultivation.Plot.Farmer.FullName.ToLower().Contains(searchTerm)));
        }

        return query;
    }

    private static ReportItemResponse MapToReportItemResponse(EmergencyReport r)
    {
        var plot = r.PlotCultivation?.Plot;
        var firstGroup = plot?.GroupPlots?.FirstOrDefault()?.Group;

        return new ReportItemResponse
        {
            Id = r.Id,
            PlotId = r.PlotCultivation?.PlotId,
            PlotName = plot != null ? $"{plot.SoThua}/{plot.SoTo}" : null,
            PlotArea = plot?.Area,
            GroupId = firstGroup?.Id,
            GroupName = firstGroup?.GroupName,
            CultivationPlanId = r.PlotCultivationId,
            CultivationPlanName = r.PlotCultivation != null
                ? $"Plan {r.PlotCultivation.PlantingDate:yyyy-MM-dd}"
                : null,
            ReportType = r.AlertType,
            Severity = r.Severity.ToString(),
            Title = r.Title,
            Description = r.Description,
            ReportedBy = r.Reporter?.FullName ?? "Unknown",
            ReportedByRole = GetReportedByRole(r.Source),
            ReportedAt = r.CreatedAt.DateTime,
            Status = r.Status.ToString(),
            Images = r.ImageUrls,
            Coordinates = r.Coordinates,
            ResolvedBy = r.Resolver?.FullName,
            ResolvedAt = r.ResolvedAt,
            ResolutionNotes = r.ResolutionNotes,
            FarmerName = plot?.Farmer?.FullName,
            ClusterName = firstGroup?.Cluster?.ClusterName
                ?? r.Group?.Cluster?.ClusterName
                ?? r.Cluster?.ClusterName,
            AffectedCultivationTaskId = r.AffectedCultivationTaskId,
            AffectedTaskName = r.AffectedTask?.CultivationTaskName
                ?? r.AffectedTask?.ProductionPlanTask?.TaskName,
            AffectedTaskType = r.AffectedTask?.TaskType?.ToString(),
            AffectedTaskVersionName = r.AffectedTask?.Version?.VersionName,
            AffectedTaskVersionId = r.AffectedTask?.VersionId
        };
    }

    private static string? GetReportedByRole(AlertSource source) => source switch
    {
        AlertSource.FarmerReport => "Farmer",
        AlertSource.SupervisorInspection => "Supervisor",
        _ => null
    };
}
