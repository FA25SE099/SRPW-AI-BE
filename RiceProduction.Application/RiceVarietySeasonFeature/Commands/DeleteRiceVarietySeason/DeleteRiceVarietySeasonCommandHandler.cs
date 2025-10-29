using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.DeleteRiceVarietySeason
{
    public class DeleteRiceVarietySeasonCommandHandler : IRequestHandler<DeleteRiceVarietySeasonCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeleteRiceVarietySeasonCommandHandler> _logger;

        public DeleteRiceVarietySeasonCommandHandler(IUnitOfWork unitOfWork, ILogger<DeleteRiceVarietySeasonCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteRiceVarietySeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietySeasonRepo = _unitOfWork.Repository<RiceVarietySeason>();

                var riceVarietySeason = await riceVarietySeasonRepo.FindAsync(rvs => rvs.Id == request.RiceVarietySeasonId);
                if (riceVarietySeason == null)
                {
                    return Result<Guid>.Failure($"Rice variety season association with ID {request.RiceVarietySeasonId} not found");
                }

                riceVarietySeasonRepo.Delete(riceVarietySeason);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Deleted rice variety season association with ID: {Id}", request.RiceVarietySeasonId);
                return Result<Guid>.Success(request.RiceVarietySeasonId, "Rice variety season association deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rice variety season association with ID: {Id}", request.RiceVarietySeasonId);
                return Result<Guid>.Failure("Failed to delete rice variety season association");
            }
        }
    }
}

