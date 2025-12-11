using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Queries.GetYearSeasonDetail;

public class GetYearSeasonDetailQueryHandler : IRequestHandler<GetYearSeasonDetailQuery, Result<YearSeasonDetailDTO>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetYearSeasonDetailQueryHandler> _logger;

    public GetYearSeasonDetailQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetYearSeasonDetailQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<YearSeasonDetailDTO>> Handle(GetYearSeasonDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeason = await _unitOfWork.Repository<YearSeason>()
                .GetQueryable()
                .Include(ys => ys.Season)
                .Include(ys => ys.Cluster)
                .Include(ys => ys.RiceVariety)
                .Include(ys => ys.ManagedByExpert)
                .Include(ys => ys.Groups)
                    .ThenInclude(g => g.Supervisor)
                .Include(ys => ys.Groups)
                    .ThenInclude(g => g.GroupPlots)
                .FirstOrDefaultAsync(ys => ys.Id == request.Id, cancellationToken);

            if (yearSeason == null)
            {
                return Result<YearSeasonDetailDTO>.Failure("YearSeason not found");
            }

            var result = new YearSeasonDetailDTO
            {
                Id = yearSeason.Id,
                SeasonId = yearSeason.SeasonId,
                SeasonName = yearSeason.Season.SeasonName,
                SeasonType = yearSeason.Season.SeasonType,
                ClusterId = yearSeason.ClusterId,
                ClusterName = yearSeason.Cluster.ClusterName,
                Year = yearSeason.Year,
                RiceVarietyId = yearSeason.RiceVarietyId,
                RiceVarietyName = yearSeason.RiceVariety.VarietyName,
                StartDate = yearSeason.StartDate,
                EndDate = yearSeason.EndDate,
                BreakStartDate = yearSeason.BreakStartDate,
                BreakEndDate = yearSeason.BreakEndDate,
                PlanningWindowStart = yearSeason.PlanningWindowStart,
                PlanningWindowEnd = yearSeason.PlanningWindowEnd,
                Status = yearSeason.Status.ToString(),
                Notes = yearSeason.Notes,
                ManagedByExpertId = yearSeason.ManagedByExpertId,
                ManagedByExpertName = yearSeason.ManagedByExpert?.FullName,
                GroupCount = yearSeason.Groups.Count,
                Groups = yearSeason.Groups.Select(g => new GroupSummaryDTO
                {
                    Id = g.Id,
                    GroupName = g.GroupName,
                    SupervisorId = g.SupervisorId,
                    SupervisorName = g.Supervisor?.FullName,
                    Status = g.Status.ToString(),
                    TotalArea = g.TotalArea,
                    PlotCount = g.GroupPlots.Count
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

