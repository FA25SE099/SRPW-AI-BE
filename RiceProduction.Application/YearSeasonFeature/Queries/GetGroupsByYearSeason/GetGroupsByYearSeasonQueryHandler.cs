using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetGroupsByYearSeason;

public class GetGroupsByYearSeasonQueryHandler : IRequestHandler<GetGroupsByYearSeasonQuery, Result<GetGroupsByYearSeasonResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetGroupsByYearSeasonQueryHandler> _logger;
    private readonly WKTWriter _wktWriter;

    public GetGroupsByYearSeasonQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetGroupsByYearSeasonQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _wktWriter = new WKTWriter();
    }

    public async Task<Result<GetGroupsByYearSeasonResponse>> Handle(
        GetGroupsByYearSeasonQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting groups for YearSeason {YearSeasonId}", request.YearSeasonId);

            // Get YearSeason with all related information
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .FindAsync(
                    match: ys => ys.Id == request.YearSeasonId,
                    includeProperties: q => q
                        .Include(ys => ys.Season)
                        .Include(ys => ys.Cluster)
                        .Include(ys => ys.RiceVariety)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.Supervisor)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.Cluster)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.GroupPlots)
                                .ThenInclude(gp => gp.Plot)
                                    .ThenInclude(p => p.Farmer)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.ProductionPlans)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.UavServiceOrders)
                        .Include(ys => ys.Groups)
                            .ThenInclude(g => g.Alerts)
                );

            if (yearSeason == null)
            {
                _logger.LogWarning("YearSeason with ID {YearSeasonId} not found", request.YearSeasonId);
                return Result<GetGroupsByYearSeasonResponse>.Failure(
                    $"YearSeason with ID {request.YearSeasonId} not found");
            }

            // Map groups to DTOs
            var groupDtos = yearSeason.Groups
                .OrderBy(g => g.PlantingDate)
                .ThenBy(g => g.CreatedAt)
                .Select(g => MapToGroupDTO(g))
                .ToList();

            // Calculate status summary
            var statusSummary = new GroupStatusSummary
            {
                DraftCount = groupDtos.Count(g => g.Status == GroupStatus.Draft),
                ActiveCount = groupDtos.Count(g => g.Status == GroupStatus.Active),
                CompletedCount = groupDtos.Count(g => g.Status == GroupStatus.Completed),
                CancelledCount = 0,
                TotalCount = groupDtos.Count
            };

            var response = new GetGroupsByYearSeasonResponse
            {
                YearSeasonId = yearSeason.Id,
                YearSeasonDisplayName = $"{yearSeason.Season.SeasonName} {yearSeason.Year}",
                ClusterId = yearSeason.ClusterId,
                ClusterName = yearSeason.Cluster.ClusterName,
                Year = yearSeason.Year,
                SeasonName = yearSeason.Season.SeasonName,
                RiceVarietyId = yearSeason.RiceVarietyId,
                RiceVarietyName = yearSeason.RiceVariety?.VarietyName,
                TotalGroupCount = groupDtos.Count,
                Groups = groupDtos,
                StatusSummary = statusSummary
            };

            _logger.LogInformation(
                "Successfully retrieved {GroupCount} groups for YearSeason {YearSeasonId}", 
                groupDtos.Count, 
                request.YearSeasonId);

            return Result<GetGroupsByYearSeasonResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving groups for YearSeason {YearSeasonId}", request.YearSeasonId);
            return Result<GetGroupsByYearSeasonResponse>.Failure(
                $"Failed to retrieve groups for YearSeason: {ex.Message}");
        }
    }

    private YearSeasonGroupDTO MapToGroupDTO(Group group)
    {
        // Get unique farmers from plots
        var uniqueFarmers = group.GroupPlots
            .Select(gp => gp.Plot?.FarmerId)
            .Where(farmerId => farmerId.HasValue)
            .Distinct()
            .Count();

        return new YearSeasonGroupDTO
        {
            GroupId = group.Id,
            GroupName = group.GroupName,
            ClusterId = group.ClusterId,
            ClusterName = group.Cluster?.ClusterName ?? string.Empty,
            SupervisorId = group.SupervisorId,
            SupervisorName = group.Supervisor?.FullName,
            YearSeasonId = group.YearSeasonId,
            Year = group.Year,
            PlantingDate = group.PlantingDate,
            Status = group.Status,
            IsException = group.IsException,
            ExceptionReason = group.ExceptionReason,
            ReadyForUavDate = group.ReadyForUavDate,
            Area = group.Area != null ? _wktWriter.Write(group.Area) : null,
            TotalArea = group.TotalArea,
            PlotCount = group.GroupPlots?.Count ?? 0,
            FarmerCount = uniqueFarmers,
            ProductionPlanCount = group.ProductionPlans?.Count ?? 0,
            UavServiceOrderCount = group.UavServiceOrders?.Count ?? 0,
            AlertCount = group.Alerts?.Count ?? 0
        };
    }
}

