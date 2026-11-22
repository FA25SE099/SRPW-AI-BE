using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.RiceVarietySeasonFeature.Commands.DeleteRiceVarietySeason
{
    public class DeleteRiceVarietySeasonCommandHandler : IRequestHandler<DeleteRiceVarietySeasonCommand, Result<Guid>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<DeleteRiceVarietySeasonCommandHandler> _logger;

        public DeleteRiceVarietySeasonCommandHandler(IApplicationDbContext context, ILogger<DeleteRiceVarietySeasonCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(DeleteRiceVarietySeasonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var riceVarietySeason = await _context.RiceVarietySeasons
                    .FirstOrDefaultAsync(rvs => rvs.Id == request.RiceVarietySeasonId, cancellationToken);
                if (riceVarietySeason == null)
                {
                    return Result<Guid>.Failure($"Rice variety season association with ID {request.RiceVarietySeasonId} not found");
                }

                _context.RiceVarietySeasons.Remove(riceVarietySeason);
                await _context.SaveChangesAsync(cancellationToken);

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

