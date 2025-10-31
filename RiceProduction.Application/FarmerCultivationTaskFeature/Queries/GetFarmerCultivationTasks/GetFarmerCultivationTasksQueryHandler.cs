using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System.Globalization;
using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Application.FarmerCultivationTaskFeature.Queries.GetFarmerCultivationTasks
{
    public class GetFarmerCultivationTasksQueryHandler :
        IRequestHandler<GetFarmerCultivationTasksQuery, Result<List<FarmerCultivationTaskResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetFarmerCultivationTasksQueryHandler> _logger;

        public GetFarmerCultivationTasksQueryHandler(
            IUnitOfWork unitOfWork,
            ILogger<GetFarmerCultivationTasksQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<List<FarmerCultivationTaskResponse>>> Handle(
            GetFarmerCultivationTasksQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var currentDate = DateTime.UtcNow.Date; 

                var cultivationTasks = await _unitOfWork.Repository<CultivationTask>().ListAsync(
                    filter: ct =>
                        ct.PlotCultivation.Plot.FarmerId == request.FarmerId &&
                        (request.PlotId == null || ct.PlotCultivation.PlotId == request.PlotId) &&
                        (request.SeasonId == null || ct.PlotCultivation.SeasonId == request.SeasonId) &&
                        (request.Status == null || ct.Status == request.Status) &&
                        (request.IncludeCompleted == true || ct.Status != TaskStatus.Completed),
                    // Date filter removed here; applied client-side below for proper parsing
                    orderBy: q => q.OrderBy(ct => ct.ScheduledEndDate),
                    includeProperties: q => q
                        .Include(ct => ct.PlotCultivation)
                            .ThenInclude(pc => pc.Plot)
                        .Include(ct => ct.PlotCultivation)
                            .ThenInclude(pc => pc.Season)
                        .Include(ct => ct.PlotCultivation)
                            .ThenInclude(pc => pc.RiceVariety)
                        .Include(ct => ct.ProductionPlanTask)
                            .ThenInclude(ppt => ppt.ProductionPlanTaskMaterials)
                                .ThenInclude(pptm => pptm.Material)
                        .Include(ct => ct.CultivationTaskMaterials)
                            .ThenInclude(ctm => ctm.Material));

                var filteredTasks = cultivationTasks
                    .Where(ct =>
                    {
                        if ((bool)request.IncludePastSeasons || ct.PlotCultivation?.Season == null)
                        {
                            return true;
                        }

                        var endDateStr = ct.PlotCultivation.Season.EndDate;
                        if (string.IsNullOrWhiteSpace(endDateStr) ||
                            !DateTime.TryParseExact(endDateStr, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDtParsed))
                        {
                            _logger.LogWarning("Invalid EndDate format in Season {SeasonId}: {EndDate}", ct.PlotCultivation.SeasonId, endDateStr);
                            return false; // Exclude invalid dates
                        }

                        // Set to current year (ParseExact defaults to year=1)
                        var endDt = endDtParsed.AddYears(currentDate.Year - 1).Date;

                        // Handle potential year wrap-around: if End < Start, assume end is next year
                        var startDateStr = ct.PlotCultivation.Season.StartDate;
                        if (DateTime.TryParseExact(startDateStr, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDtParsed))
                        {
                            var startDt = startDtParsed.AddYears(currentDate.Year - 1).Date;
                            if (endDt < startDt)
                            {
                                endDt = endDt.AddYears(1);
                            }
                        }

                        return endDt >= currentDate;
                    })
                    .ToList();

                var response = filteredTasks.Select(ct => new FarmerCultivationTaskResponse
                {
                    TaskId = ct.Id,
                    TaskName = ct.CultivationTaskName ?? ct.ProductionPlanTask.TaskName,
                    Description = ct.Description ?? ct.ProductionPlanTask.Description,
                    TaskType = ct.TaskType ?? ct.ProductionPlanTask.TaskType,
                    Status = ct.Status,
                    ScheduledEndDate = ct.ScheduledEndDate,
                    ActualStartDate = ct.ActualStartDate,
                    ActualEndDate = ct.ActualEndDate,
                    IsContingency = ct.IsContingency,
                    ContingencyReason = ct.ContingencyReason,
                    ActualMaterialCost = ct.ActualMaterialCost,
                    ActualServiceCost = ct.ActualServiceCost,

                    Plot = new PlotInfo
                    {
                        PlotId = ct.PlotCultivation.PlotId,
                        SoThua = ct.PlotCultivation.Plot.SoThua,
                        SoTo = ct.PlotCultivation.Plot.SoTo,
                        Area = ct.PlotCultivation.Plot.Area,
                        SoilType = ct.PlotCultivation.Plot.SoilType
                    },

                    Cultivation = new CultivationInfo
                    {
                        PlotCultivationId = ct.PlotCultivationId,
                        SeasonName = ct.PlotCultivation.Season.SeasonName,
                        RiceVarietyName = ct.PlotCultivation.RiceVariety.VarietyName,
                        PlantingDate = ct.PlotCultivation.PlantingDate,
                        Status = ct.PlotCultivation.Status
                    },

                    Materials = ct.ProductionPlanTask.ProductionPlanTaskMaterials
                        .Select(pptm =>
                        {
                            var actualMaterial = ct.CultivationTaskMaterials
                                .FirstOrDefault(ctm => ctm.MaterialId == pptm.MaterialId);

                            return new TaskMaterialInfo
                            {
                                MaterialId = pptm.MaterialId,
                                MaterialName = pptm.Material.Name,
                                MaterialType = pptm.Material.Type,
                                PlannedQuantity =(decimal) pptm.EstimatedAmount,
                                PlannedCost = 0,
                                ActualQuantity = actualMaterial?.ActualQuantity,
                                ActualCost = actualMaterial?.ActualCost,
                                Unit = pptm.Material.Unit,
                                IsUsed = actualMaterial != null
                            };
                        }).ToList()
                }).ToList();

                _logger.LogInformation(
                    "Retrieved {Count} cultivation tasks for farmer {FarmerId} after filtering",
                    response.Count,
                    request.FarmerId);

                return Result<List<FarmerCultivationTaskResponse>>.Success(
                    response,
                    $"Successfully retrieved {response.Count} cultivation tasks");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cultivation tasks for farmer {FarmerId}", request.FarmerId);
                return Result<List<FarmerCultivationTaskResponse>>.Failure(
                    "An error occurred while retrieving cultivation tasks");
            }
        }

        private bool IsSeasonCurrent(Season season, DateTime currentDate)
        {
            var endDateStr = season.EndDate;
            if (string.IsNullOrWhiteSpace(endDateStr) ||
                !DateTime.TryParseExact(endDateStr, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDtParsed))
            {
                return false;
            }

            var endDt = endDtParsed.AddYears(currentDate.Year - 1).Date;

            var startDateStr = season.StartDate;
            if (DateTime.TryParseExact(startDateStr, "MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDtParsed))
            {
                var startDt = startDtParsed.AddYears(currentDate.Year - 1).Date;
                if (endDt < startDt)
                {
                    endDt = endDt.AddYears(1);
                }
            }

            return endDt >= currentDate;
        }
    }
}