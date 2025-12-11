using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeason;

public class UpdateYearSeasonCommandHandler : IRequestHandler<UpdateYearSeasonCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateYearSeasonCommandHandler> _logger;

    public UpdateYearSeasonCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateYearSeasonCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateYearSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeasonRepo = _unitOfWork.Repository<YearSeason>();
            var yearSeason = await yearSeasonRepo.GetEntityByIdAsync(request.Id);

            if (yearSeason == null)
            {
                return Result<bool>.Failure("YearSeason not found");
            }

            if (request.RiceVarietyId.HasValue)
            {
                var riceVariety = await _unitOfWork.Repository<RiceVariety>().GetEntityByIdAsync(request.RiceVarietyId.Value);
                if (riceVariety == null)
                {
                    return Result<bool>.Failure("Rice variety not found");
                }
                yearSeason.RiceVarietyId = request.RiceVarietyId.Value;
            }

            if (request.StartDate.HasValue)
                yearSeason.StartDate = request.StartDate.Value;

            if (request.EndDate.HasValue)
                yearSeason.EndDate = request.EndDate.Value;

            if (request.BreakStartDate.HasValue)
                yearSeason.BreakStartDate = request.BreakStartDate;

            if (request.BreakEndDate.HasValue)
                yearSeason.BreakEndDate = request.BreakEndDate;

            if (request.PlanningWindowStart.HasValue)
                yearSeason.PlanningWindowStart = request.PlanningWindowStart;

            if (request.PlanningWindowEnd.HasValue)
                yearSeason.PlanningWindowEnd = request.PlanningWindowEnd;

            if (request.Notes != null)
                yearSeason.Notes = request.Notes;

            yearSeasonRepo.Update(yearSeason);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Updated YearSeason with ID: {YearSeasonId}", request.Id);
            return Result<bool>.Success(true, "YearSeason updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating YearSeason");
            return Result<bool>.Failure("Failed to update YearSeason");
        }
    }
}

