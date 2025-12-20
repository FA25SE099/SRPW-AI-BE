using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.PlotFeature.Queries.GetByFarmerId;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.PlotFeature.Queries.GetPlotsByFarmer;

public class GetPlotsByFarmerQueryHandler : IRequestHandler<GetPlotsByFarmerQuery, PagedResult<List<PlotListResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPlotsByFarmerQueryHandler> _logger;

    public GetPlotsByFarmerQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPlotsByFarmerQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<PlotListResponse>>> Handle(
        GetPlotsByFarmerQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify farmer exists using FarmerRepository
            var farmer = await _unitOfWork.FarmerRepository.GetFarmerByIdAsync(request.FarmerId, cancellationToken);

            if (farmer == null)
            {
                return PagedResult<List<PlotListResponse>>.Failure(
                    "Farmer not found.",
                    "NotFound");
            }

            // Build filter
            var query = _unitOfWork.Repository<Plot>().GetQueryable()
                .Where(p => p.FarmerId == request.FarmerId);


            // Include related data
            query = query
                .Include(p => p.GroupPlots)
                    .ThenInclude(gp => gp.Group)
                .Include(p => p.PlotCultivations)
                .Include(p => p.Alerts);

            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var plots = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Map to response
            var plotListResponses = plots.Select(p =>
            {
                var latestGroupPlot = p.GroupPlots
                    .OrderByDescending(gp => gp.CreatedAt)
                    .FirstOrDefault();

                var activeCultivations = p.PlotCultivations.Count(pc =>
                    pc.Status == CultivationStatus.Planned || pc.Status == CultivationStatus.InProgress);

                var activeAlerts = p.Alerts.Count(a =>
                    a.Status == AlertStatus.Pending);

                return new PlotListResponse
                {
                    PlotId = p.Id,
                    Area = p.Area,
                    SoThua = p.SoThua,
                    SoTo = p.SoTo,
                    Status = p.Status,
                    GroupId = latestGroupPlot?.GroupId,
                    Boundary = p.Boundary?.AsText(),
                    Coordinate = p.Coordinate?.AsText(),
                    GroupName = latestGroupPlot?.Group?.GroupName,
                    ActiveCultivations = activeCultivations,
                    ActiveAlerts = activeAlerts
                };
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} plots for farmer {FarmerId}",
                plotListResponses.Count,
                request.FarmerId);

            return PagedResult<List<PlotListResponse>>.Success(
                plotListResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved plots.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving plots for farmer {FarmerId}",
                request.FarmerId);
            return PagedResult<List<PlotListResponse>>.Failure(
                "An error occurred while retrieving plots.",
                "GetPlotsFailed");
        }
    }
}
