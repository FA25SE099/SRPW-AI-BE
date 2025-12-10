using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.LateFarmerRecordFeature.Queries.GetLatePlotsInCluster;

public class GetLatePlotsInClusterQueryHandler : IRequestHandler<GetLatePlotsInClusterQuery, PagedResult<IEnumerable<PlotWithLateCountDTO>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetLatePlotsInClusterQueryHandler> _logger;

    public GetLatePlotsInClusterQueryHandler(IUnitOfWork unitOfWork, ILogger<GetLatePlotsInClusterQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<IEnumerable<PlotWithLateCountDTO>>> Handle(GetLatePlotsInClusterQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting plots with late records in cluster");

            List<Guid> plotIds = new List<Guid>();

            if (request.AgronomyExpertId.HasValue)
            {
                // Get all plots in the cluster that the agronomy expert manages
                var expert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(request.AgronomyExpertId.Value, cancellationToken);
                if (expert == null)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Agronomy Expert not found");
                }

                if (!expert.ClusterId.HasValue)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Agronomy Expert is not assigned to any cluster");
                }

                // Get all late records in the cluster
                var lateRecordsInCluster = await _unitOfWork.LateFarmerRecordRepository.GetQueryable()
                    .Where(lr => lr.ClusterId == expert.ClusterId.Value)
                    .Select(lr => lr.PlotId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                plotIds = lateRecordsInCluster;
            }
            else if (request.SupervisorId.HasValue)
            {
                // Get plots only from groups that the supervisor is currently managing
                var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(request.SupervisorId.Value, cancellationToken);
                if (supervisor == null)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Supervisor not found");
                }

                if (!supervisor.ClusterId.HasValue)
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Supervisor is not assigned to any cluster");
                }

                // Get groups managed by this supervisor
                var groupIds = await _unitOfWork.Repository<Domain.Entities.Group>()
                    .GetQueryable()
                    .Where(g => g.SupervisorId == request.SupervisorId.Value)
                    .Select(g => g.Id)
                    .ToListAsync(cancellationToken);

                if (!groupIds.Any())
                {
                    return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                        data: new List<PlotWithLateCountDTO>(),
                        currentPage: request.PageNumber,
                        pageSize: request.PageSize,
                        totalCount: 0,
                        message: "No groups found for this supervisor"
                    );
                }

                // Get plots with late records in these groups
                plotIds = await _unitOfWork.LateFarmerRecordRepository.GetQueryable()
                    .Where(lr => groupIds.Contains(lr.GroupId))
                    .Select(lr => lr.PlotId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }
            else
            {
                return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("Either AgronomyExpertId or SupervisorId must be provided");
            }

            if (!plotIds.Any())
            {
                return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                    data: new List<PlotWithLateCountDTO>(),
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: 0,
                    message: "No plots with late records found"
                );
            }

            // Build plot query
            var plotQuery = _unitOfWork.PlotRepository.PlotQueryable()
                .Include(p => p.Farmer)
                .Where(p => plotIds.Contains(p.Id));

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                plotQuery = plotQuery.Where(p =>
                    (p.Farmer.FullName != null && p.Farmer.FullName.Contains(request.SearchTerm)) ||
                    (p.SoThua.HasValue && p.SoThua.ToString().Contains(request.SearchTerm)) ||
                    (p.SoTo.HasValue && p.SoTo.ToString().Contains(request.SearchTerm)));
            }

            // Get total count
            var totalCount = await plotQuery.CountAsync(cancellationToken);

            // Get paged plots
            var plots = await plotQuery
                .OrderBy(p => p.Farmer.FullName)
                .ThenBy(p => p.SoThua)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // Get late counts for each plot
            var result = new List<PlotWithLateCountDTO>();
            foreach (var plot in plots)
            {
                var lateCount = await _unitOfWork.LateFarmerRecordRepository.GetLateCountByPlotIdAsync(plot.Id, cancellationToken);
                
                // Get group ID (get the most recent group assignment)
                var groupId = await _unitOfWork.Repository<Domain.Entities.GroupPlot>()
                    .GetQueryable()
                    .Where(gp => gp.PlotId == plot.Id)
                    .OrderByDescending(gp => gp.CreatedAt)
                    .Select(gp => (Guid?)gp.GroupId)
                    .FirstOrDefaultAsync(cancellationToken);

                result.Add(new PlotWithLateCountDTO
                {
                    PlotId = plot.Id,
                    FarmerId = plot.FarmerId,
                    FarmerName = plot.Farmer?.FullName,
                    GroupId = groupId,
                    SoThua = plot.SoThua,
                    SoTo = plot.SoTo,
                    Area = plot.Area,
                    SoilType = plot.SoilType,
                    Status = plot.Status,
                    LateCount = lateCount
                });
            }

            return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Success(
                data: result,
                currentPage: request.PageNumber,
                pageSize: request.PageSize,
                totalCount: totalCount,
                message: "Plots with late records retrieved successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting plots with late records in cluster");
            return PagedResult<IEnumerable<PlotWithLateCountDTO>>.Failure("An error occurred while processing your request");
        }
    }
}
