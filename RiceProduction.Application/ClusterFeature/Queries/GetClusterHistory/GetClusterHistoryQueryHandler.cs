using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ClusterHistoryResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ClusterFeature.Queries.GetClusterHistory;

public class GetClusterHistoryQueryHandler 
    : IRequestHandler<GetClusterHistoryQuery, Result<ClusterHistoryResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetClusterHistoryQueryHandler> _logger;

    public GetClusterHistoryQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetClusterHistoryQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ClusterHistoryResponse>> Handle(
        GetClusterHistoryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get cluster (regular entity)
            var cluster = await _unitOfWork.Repository<Cluster>()
                .FindAsync(c => c.Id == request.ClusterId);

            if (cluster == null)
            {
                return Result<ClusterHistoryResponse>.Failure(
                    $"Cluster with ID {request.ClusterId} not found");
            }

            // Get all seasons (regular entity)
            var allSeasons = await _unitOfWork.Repository<Season>()
                .ListAsync(_ => true);

            // Filter seasons based on request
            var targetSeasons = allSeasons.AsEnumerable();

            if (request.SeasonId.HasValue)
            {
                targetSeasons = targetSeasons.Where(s => s.Id == request.SeasonId.Value);
            }

            if (request.Year.HasValue)
            {
                // Filter by year - need to determine which seasons belong to this year
                targetSeasons = targetSeasons.Where(s => 
                    GetSeasonYear(s, request.Year.Value) == request.Year.Value);
            }

            // Order by most recent and apply limit
            targetSeasons = targetSeasons
                .OrderByDescending(s => s.CreatedAt)
                .Take(request.Limit ?? 5)
                .ToList();

            var seasonSnapshots = new List<ClusterSeasonSnapshot>();

            foreach (var season in targetSeasons)
            {
                var year = GetSeasonYear(season, DateTime.Now.Year);

                // Get all groups for this cluster and season (regular entity)
                var groups = await _unitOfWork.Repository<Group>()
                    .GetQueryable()
                    .Include(g => g.YearSeason)
                    .Where(g => g.ClusterId == cluster.Id && 
                               g.YearSeason != null && g.YearSeason.SeasonId == season.Id &&
                               g.Year == year)
                    .ToListAsync(cancellationToken);

                var groupsList = groups.ToList();

                if (!groupsList.Any())
                {
                    // Include seasons with no groups
                    seasonSnapshots.Add(new ClusterSeasonSnapshot
                    {
                        SeasonId = season.Id,
                        SeasonName = season.SeasonName,
                        SeasonType = season.SeasonType ?? "",
                        Year = year,
                        StartDate = season.StartDate,
                        EndDate = season.EndDate,
                        IsActive = season.IsActive,
                        TotalGroups = 0,
                        TotalPlots = 0,
                        TotalFarmers = 0,
                        TotalArea = 0
                    });
                    continue;
                }

                // Get plots for these groups using many-to-many relationship
                var groupIds = groupsList.Select(g => g.Id).ToList();
                var groupPlots = await _unitOfWork.Repository<GroupPlot>()
                    .ListAsync(gp => groupIds.Contains(gp.GroupId),
                        includeProperties: q => q.Include(gp => gp.Plot));
                var plotsList = groupPlots.Select(gp => gp.Plot).ToList();

                // Get farmers (user entity - use specialized repository)
                var farmerIds = plotsList.Select(p => p.FarmerId).Distinct().ToList();
                var farmers = await _unitOfWork.FarmerRepository
                    .ListAsync(f => farmerIds.Contains(f.Id));

                // Get plot cultivations for yield data
                var plotIds = plotsList.Select(p => p.Id).ToList();
                var plotCultivations = await _unitOfWork.Repository<PlotCultivation>()
                    .ListAsync(pc => plotIds.Contains(pc.PlotId) && 
                                    pc.SeasonId == season.Id);
                var cultivationsList = plotCultivations.ToList();

                // Get rice varieties
                var varietyIds = groupsList
                    .Where(g => g.YearSeason?.RiceVarietyId != null)
                    .Select(g => g.YearSeason!.RiceVarietyId)
                    .Distinct()
                    .ToList();
                
                var riceVarieties = await _unitOfWork.Repository<RiceVariety>()
                    .ListAsync(rv => varietyIds.Contains(rv.Id));
                var varietyDict = riceVarieties.ToDictionary(rv => rv.Id);

                // Get supervisors (user entity - use specialized repository)
                var supervisorIds = groupsList
                    .Where(g => g.SupervisorId.HasValue)
                    .Select(g => g.SupervisorId!.Value)
                    .Distinct()
                    .ToList();
                
                var supervisors = await _unitOfWork.SupervisorRepository
                    .ListAsync(s => supervisorIds.Contains(s.Id));
                var supervisorDict = supervisors.ToDictionary(s => s.Id);

                // Get production plans
                var productionPlans = await _unitOfWork.Repository<ProductionPlan>()
                    .ListAsync(pp => pp.GroupId.HasValue && groupIds.Contains(pp.GroupId.Value));

                // Calculate rice variety breakdown
                var varietyBreakdown = groupsList
                    .Where(g => g.YearSeason?.RiceVarietyId != null)
                    .GroupBy(g => g.YearSeason!.RiceVarietyId!.Value)
                    .Select(g => new RiceVarietyGroupSummary
                    {
                        RiceVarietyId = g.Key,
                        RiceVarietyName = varietyDict.GetValueOrDefault(g.Key)?.VarietyName ?? "Unknown",
                        GroupCount = g.Count(),
                        PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => g.Select(gr => gr.Id).Contains(gp.GroupId))),
                        TotalArea = g.Sum(gr => gr.TotalArea ?? 0)
                    })
                    .OrderByDescending(v => v.PlotCount)
                    .ToList();

                // Calculate metrics
                var averageYield = cultivationsList
                    .Where(pc => pc.ActualYield.HasValue)
                    .Average(pc => (decimal?)pc.ActualYield);

                var totalProduction = cultivationsList
                    .Where(pc => pc.ActualYield.HasValue && pc.Area.HasValue)
                    .Sum(pc => pc.ActualYield!.Value * pc.Area!.Value);

                // Build group summaries
                var groupSummaries = groupsList.Select(g => new GroupSeasonSummary
                {
                    GroupId = g.Id,
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.SupervisorId.HasValue 
                        ? supervisorDict.GetValueOrDefault(g.SupervisorId.Value)?.FullName 
                        : null,
                    RiceVarietyId = g.YearSeason?.RiceVarietyId,
                    RiceVarietyName = g.YearSeason?.RiceVarietyId != null 
                        ? varietyDict.GetValueOrDefault(g.YearSeason.RiceVarietyId.Value)?.VarietyName 
                        : null,
                    PlantingDate = g.PlantingDate,
                    Status = g.Status.ToString(),
                    PlotCount = plotsList.Count(p => p.GroupPlots.Any(gp => gp.GroupId == g.Id)),
                    TotalArea = g.TotalArea
                }).ToList();

                seasonSnapshots.Add(new ClusterSeasonSnapshot
                {
                    SeasonId = season.Id,
                    SeasonName = season.SeasonName,
                    SeasonType = season.SeasonType ?? "",
                    Year = year,
                    StartDate = season.StartDate,
                    EndDate = season.EndDate,
                    IsActive = season.IsActive,
                    TotalGroups = groupsList.Count,
                    TotalPlots = plotsList.Count,
                    TotalFarmers = farmers.Count(),
                    TotalArea = groupsList.Sum(g => g.TotalArea ?? 0),
                    RiceVarietyBreakdown = varietyBreakdown,
                    AverageYield = averageYield,
                    TotalProduction = totalProduction,
                    CompletedProductionPlans = productionPlans.Count(pp => pp.Status == Domain.Enums.TaskStatus.Completed),
                    Groups = groupSummaries
                });
            }

            _logger.LogInformation(
                "Retrieved {SnapshotCount} season snapshots for cluster {ClusterId}",
                seasonSnapshots.Count, cluster.Id);

            return Result<ClusterHistoryResponse>.Success(new ClusterHistoryResponse
            {
                ClusterId = cluster.Id,
                ClusterName = cluster.ClusterName,
                SeasonSnapshots = seasonSnapshots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving cluster history for cluster {ClusterId}", 
                request.ClusterId);
            return Result<ClusterHistoryResponse>.Failure(
                $"Error retrieving cluster history: {ex.Message}");
        }
    }

    private int GetSeasonYear(Season season, int currentYear)
    {
        try
        {
            var startParts = season.StartDate.Split('/');
            int startMonth = int.Parse(startParts[0]);
            
            // If season starts in later months (Oct-Dec), it likely continues into next year
            // For current date in early months, the season year would be previous year
            if (startMonth >= 10)
            {
                var currentMonth = DateTime.Now.Month;
                if (currentMonth < startMonth)
                {
                    return currentYear - 1;
                }
            }
            
            return currentYear;
        }
        catch
        {
            return currentYear;
        }
    }
}

