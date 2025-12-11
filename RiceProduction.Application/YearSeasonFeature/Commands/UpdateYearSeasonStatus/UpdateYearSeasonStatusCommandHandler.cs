using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Commands.UpdateYearSeasonStatus;

public class UpdateYearSeasonStatusCommandHandler : IRequestHandler<UpdateYearSeasonStatusCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateYearSeasonStatusCommandHandler> _logger;

    public UpdateYearSeasonStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateYearSeasonStatusCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateYearSeasonStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeasonRepo = _unitOfWork.Repository<YearSeason>();
            var yearSeason = await yearSeasonRepo.GetEntityByIdAsync(request.Id);

            if (yearSeason == null)
            {
                return Result<bool>.Failure("YearSeason not found");
            }

            yearSeason.Status = request.Status;
            yearSeasonRepo.Update(yearSeason);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Updated YearSeason status to {Status} for ID: {YearSeasonId}", request.Status, request.Id);
            return Result<bool>.Success(true, $"YearSeason status updated to {request.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating YearSeason status");
            return Result<bool>.Failure("Failed to update YearSeason status");
        }
    }
}

