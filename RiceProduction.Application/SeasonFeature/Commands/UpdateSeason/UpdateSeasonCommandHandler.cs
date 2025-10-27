using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.SeasonFeature.Commands.UpdateSeason
{
    public class UpdateSeasonCommandHandler : IRequestHandler<UpdateSeasonCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateSeasonCommandHandler> _logger;

        public UpdateSeasonCommandHandler(IUnitOfWork unitOfWork, ILogger<UpdateSeasonCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(UpdateSeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var seasonRepo = _unitOfWork.Repository<Season>();

                var season = await seasonRepo.FindAsync(s => s.Id == request.SeasonId);
                if (season == null)
                {
                    return Result<Guid>.Failure($"Season with ID {request.SeasonId} not found");
                }

                season.SeasonName = request.SeasonName;
                season.StartDate = request.StartDate;
                season.EndDate = request.EndDate;
                season.SeasonType = request.SeasonType;
                season.IsActive = request.IsActive;

                seasonRepo.Update(season);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Updated season with ID: {SeasonId}", request.SeasonId);
                return Result<Guid>.Success(request.SeasonId, "Season updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating season with ID: {SeasonId}", request.SeasonId);
                return Result<Guid>.Failure("Failed to update season");
            }
        }
    }
}

