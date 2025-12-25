using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.ReportFeature.Queries.GetAllReports;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.ReportFeature.Queries.GetReportsBySupervisor;

public class GetReportsBySupervisorQueryHandler : IRequestHandler<GetReportsBySupervisorQuery, PagedResult<List<ReportItemResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetReportsBySupervisorQueryHandler> _logger;

    public GetReportsBySupervisorQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetReportsBySupervisorQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<ReportItemResponse>>> Handle(
        GetReportsBySupervisorQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify supervisor exists using SupervisorRepository
            var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(request.SupervisorId, cancellationToken);

            if (supervisor == null)
            {
                return PagedResult<List<ReportItemResponse>>.Failure(
                    "Supervisor not found.",
                    "NotFound");
            }

            // Get all groups supervised by this supervisor
            var groups = await _unitOfWork.Repository<Group>()
                .ListAsync(
                    filter: g => g.SupervisorId == request.SupervisorId,
                    includeProperties: q => q.Include(g => g.GroupPlots)
                        .ThenInclude(gp => gp.Plot)
                );

            if (!groups.Any())
            {
                return PagedResult<List<ReportItemResponse>>.Success(
                    new List<ReportItemResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No groups assigned to this supervisor.");
            }

            // Get all plot IDs from supervised groups
            var supervisedPlotIds = groups
                .SelectMany(g => g.GroupPlots)
                .Select(gp => gp.PlotId)
                .Distinct()
                .ToList();

            if (!supervisedPlotIds.Any())
            {
                return PagedResult<List<ReportItemResponse>>.Success(
                    new List<ReportItemResponse>(),
                    request.CurrentPage,
                    request.PageSize,
                    0,
                    "No plots found in supervised groups.");
            }

            // Build query for reports from these plots
            var query = _unitOfWork.Repository<EmergencyReport>()
                .GetQueryable()
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
                    .ThenInclude(t => t.Version)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (Enum.TryParse<AlertStatus>(request.Status, true, out var statusEnum))
                {
                    query = query.Where(r => r.Status == statusEnum);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Severity))
            {
                if (Enum.TryParse<AlertSeverity>(request.Severity, true, out var severityEnum))
                {
                    query = query.Where(r => r.Severity == severityEnum);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.ReportType))
            {
                var reportType = request.ReportType.Trim();
                query = query.Where(r => r.AlertType.ToLower() == reportType.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower().Trim();
                query = query.Where(r =>
                    r.Title.ToLower().Contains(searchTerm) ||
                    r.Description.ToLower().Contains(searchTerm) ||
                    (r.PlotCultivation != null && r.PlotCultivation.Plot != null && r.PlotCultivation.Plot.Farmer != null &&
                        r.PlotCultivation.Plot.Farmer.FullName.ToLower().Contains(searchTerm))
                );
            }

            // Order by newest first
            query = query.OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var pagedReports = await query
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to response
            var responseData = pagedReports.Select(r => new ReportItemResponse
            {
                Id = r.Id,
                PlotId = r.PlotCultivation?.PlotId,
                PlotName = r.PlotCultivation?.Plot != null
                    ? $"{r.PlotCultivation.Plot.SoThua}/{r.PlotCultivation.Plot.SoTo}"
                    : null,
                PlotArea = r.PlotCultivation?.Plot?.Area,
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
                FarmerName = r.PlotCultivation?.Plot?.Farmer?.FullName,
                ClusterName = r.PlotCultivation?.Plot?.GroupPlots?.FirstOrDefault()?.Group?.Cluster?.ClusterName
                    ?? r.Group?.Cluster?.ClusterName
                    ?? r.Cluster?.ClusterName,
                AffectedCultivationTaskId = r.AffectedCultivationTaskId,
                AffectedTaskName = r.AffectedTask?.CultivationTaskName 
                    ?? r.AffectedTask?.ProductionPlanTask?.TaskName,
                AffectedTaskType = r.AffectedTask?.TaskType?.ToString(),
                AffectedTaskVersionName = r.AffectedTask?.Version?.VersionName
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} reports for supervisor {SupervisorId} (from {PlotCount} plots in {GroupCount} groups)",
                responseData.Count,
                request.SupervisorId,
                supervisedPlotIds.Count,
                groups.Count);

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
