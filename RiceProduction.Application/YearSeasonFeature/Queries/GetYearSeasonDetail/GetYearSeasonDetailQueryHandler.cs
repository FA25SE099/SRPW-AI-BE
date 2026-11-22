using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDetail;

public class GetYearSeasonDetailQueryHandler : IRequestHandler<GetYearSeasonDetailQuery, Result<YearSeasonDetailDTO>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetYearSeasonDetailQueryHandler> _logger;

    public GetYearSeasonDetailQueryHandler(
        IApplicationDbContext context,
        ILogger<GetYearSeasonDetailQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<YearSeasonDetailDTO>> Handle(GetYearSeasonDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _context.YearSeasons
                .AsNoTracking()
                .AsSplitQuery()
                .Where(ys => ys.Id == request.Id)
                .Select(ys => new
                {
                    ys.Id,
                    ys.SeasonId,
                    ys.Season.SeasonName,
                    ys.Season.SeasonType,
                    ys.ClusterId,
                    ys.Cluster.ClusterName,
                    ys.Year,
                    ys.RiceVarietyId,
                    RiceVarietyName = ys.RiceVariety != null ? ys.RiceVariety.VarietyName : null,
                    ys.StartDate,
                    ys.EndDate,
                    ys.BreakStartDate,
                    ys.BreakEndDate,
                    ys.PlanningWindowStart,
                    ys.PlanningWindowEnd,
                    ys.Status,
                    ys.Notes,
                    ys.ManagedByExpertId,
                    ManagedByExpertName = ys.ManagedByExpert != null ? ys.ManagedByExpert.FullName : null,
                    Groups = ys.Groups.Select(g => new
                    {
                        g.Id,
                        g.GroupName,
                        g.SupervisorId,
                        SupervisorName = g.Supervisor != null ? g.Supervisor.FullName : null,
                        g.Status,
                        g.TotalArea,
                        PlotCount = g.GroupPlots.Count
                    }).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonDetailDTO>.Failure("YearSeason not found");
            }

            var result = new YearSeasonDetailDTO
            {
                Id = yearSeason.Id,
                SeasonId = yearSeason.SeasonId,
                SeasonName = yearSeason.SeasonName,
                SeasonType = yearSeason.SeasonType,
                ClusterId = yearSeason.ClusterId,
                ClusterName = yearSeason.ClusterName,
                Year = yearSeason.Year,
                RiceVarietyId = yearSeason.RiceVarietyId,
                RiceVarietyName = yearSeason.RiceVarietyName,
                StartDate = yearSeason.StartDate,
                EndDate = yearSeason.EndDate,
                BreakStartDate = yearSeason.BreakStartDate,
                BreakEndDate = yearSeason.BreakEndDate,
                PlanningWindowStart = yearSeason.PlanningWindowStart,
                PlanningWindowEnd = yearSeason.PlanningWindowEnd,
                Status = yearSeason.Status.ToString(),
                Notes = yearSeason.Notes,
                ManagedByExpertId = yearSeason.ManagedByExpertId,
                ManagedByExpertName = yearSeason.ManagedByExpertName,
                GroupCount = yearSeason.Groups.Count,
                Groups = yearSeason.Groups.Select(g => new GroupSummaryDTO
                {
                    Id = g.Id,
                    GroupName = g.GroupName,
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.SupervisorName,
                    Status = g.Status.ToString(),
                    TotalArea = g.TotalArea,
                    PlotCount = g.PlotCount
                }).ToList()
            };

            return Result<YearSeasonDetailDTO>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving YearSeason detail for ID {Id}", request.Id);
            return Result<YearSeasonDetailDTO>.Failure("Failed to retrieve YearSeason detail");
        }
    }
}

