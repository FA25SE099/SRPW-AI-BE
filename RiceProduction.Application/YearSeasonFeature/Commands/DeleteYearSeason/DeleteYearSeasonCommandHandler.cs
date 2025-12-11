using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.YearSeasonFeature.Commands.DeleteYearSeason;

public class DeleteYearSeasonCommandHandler : IRequestHandler<DeleteYearSeasonCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteYearSeasonCommandHandler> _logger;

    public DeleteYearSeasonCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteYearSeasonCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteYearSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeasonRepo = _unitOfWork.Repository<YearSeason>();
            var yearSeason = await yearSeasonRepo.GetEntityByIdAsync(request.Id);

            if (yearSeason == null)
            {
                return Result<bool>.Failure("YearSeason not found");
            }

            var hasGroups = await _unitOfWork.Repository<Group>()
                .ExistsAsync(g => g.YearSeasonId == request.Id);

            if (hasGroups)
            {
                return Result<bool>.Failure("Cannot delete YearSeason that has associated groups");
            }

            yearSeasonRepo.Delete(yearSeason);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Deleted YearSeason with ID: {YearSeasonId}", request.Id);
            return Result<bool>.Success(true, "YearSeason deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting YearSeason");
            return Result<bool>.Failure("Failed to delete YearSeason");
        }
    }
}

