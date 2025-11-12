using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Index.HPRtree;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.PlotFeature.Queries.GetOutOfSeason
{
    public class GetPlotOutSeasonQueriesHandler : IRequestHandler<GetPlotOutSeasonQueries, Result<IEnumerable<PlotDTO>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetPlotOutSeasonQueriesHandler> _logger;

        public GetPlotOutSeasonQueriesHandler(IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetPlotOutSeasonQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<IEnumerable<PlotDTO>>> Handle(GetPlotOutSeasonQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting all plots out of this season - Current date: {Date}, SearchTerm:{Search}",
                    request.CurrentDate,
                    request.SearchTerm);
                var currentDate = request.CurrentDate?.ToUniversalTime() ?? DateTime.UtcNow;
                var query = _unitOfWork.Repository<Plot>()
                    .GetQueryable()
                    .Include(p => p.Farmer)
                    .Include(p => p.Group).ThenInclude(g => g.RiceVariety)
                    .Include(p => p.PlotCultivations).Where(p => p.Status == Domain.Enums.PlotStatus.Active)
                    .Include (p =>p.PlotCultivations).ThenInclude(pc => pc.Season)
                    .AsQueryable();
                //filter:
                query = query
                //khong co plot cultivation
                .Where(p => !p.PlotCultivations.Any() ||
                //ko co plotcultivation nao dang lam
                !p.PlotCultivations.Any(pc => pc.Status == Domain.Enums.CultivationStatus.Planned ||
                pc.Status == Domain.Enums.CultivationStatus.InProgress) ||
                //tat ca cac plotcultivation da ket thuc
                p.PlotCultivations.All(pc => pc.Status == Domain.Enums.CultivationStatus.Completed ||
                pc.Status == Domain.Enums.CultivationStatus.Failed) ||
                //ngay gieo trong trong qua khu
                p.PlotCultivations.All(pc => pc.PlantingDate < currentDate));


                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    var searchTerm = request.SearchTerm.ToLower();
                    query = query.Where(p =>
                        p.SoThua.HasValue && p.SoThua.ToString().Contains(searchTerm) ||
                        p.SoTo.HasValue && p.SoTo.ToString().Contains(searchTerm) ||
                        p.Farmer != null && p.Farmer.FullName.ToLower().Contains(searchTerm) ||
                        p.SoilType != null && p.SoilType.ToLower().Contains(searchTerm)
                    );
                }
                var items = await query
               .OrderBy(p => p.SoThua)
               .ThenBy(p => p.SoTo)
               .ToListAsync(cancellationToken);

                var filterdItems = items.Where(p =>
                {
                    if (!p.PlotCultivations.Any())
                    {
                        return true;
                    }
                    return p.PlotCultivations.All(pc => !IsDateInSeason(pc.Season, currentDate));
                }
                ).ToList();

                var plotDTOs = _mapper.Map<IEnumerable<PlotDTO>>(filterdItems);

                _logger.LogInformation("Found {Count} plots out of season", items.Count);

                return Result<IEnumerable<PlotDTO>>.Success(
                    plotDTOs,
                    "Plots out of season retrieved successfully");
            }   
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting plots out of season");
                return Result<IEnumerable<PlotDTO>>.Failure(
                    ex.Message,
                    "Failed to retrieve plots out of season");
            }
        }
        private bool IsDateInSeason(Season season, DateTime currentDate)
        {
            if (season == null)
                return false;

            try
            {
                if (string.IsNullOrEmpty(season.StartDate) || string.IsNullOrWhiteSpace(season.EndDate))
                    return false;

                if (!DateTime.TryParseExact(season.StartDate, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDateParsed) ||
                    !DateTime.TryParseExact(season.EndDate, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDateParsed))
                {
                    _logger.LogWarning("Invalid date format in Season {SeasonId}. StartDate: {StartDate}, EndDate: {EndDate}",
                        season.Id, season.StartDate, season.EndDate);
                    return false;
                }

                var startDate = new DateTime(currentDate.Year, startDateParsed.Month, startDateParsed.Day);
                var endDate = new DateTime(currentDate.Year, endDateParsed.Month, endDateParsed.Day);

                if (endDate < startDate)
                    endDate = endDate.AddYears(1);

                return currentDate.Date >= startDate.Date && currentDate.Date <= endDate.Date;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if date is in season {SeasonId}", season.Id);
                return false;
            }
        }
    }
}
