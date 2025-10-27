using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
namespace RiceProduction.Application.RiceVarietyFeature.Commands.ChangeRiceSeason;

public class ChangeRiceSeasonCommandHandler: IRequestHandler <ChangeRiceSeasonCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangeRiceSeasonCommandHandler> _logger;

    public ChangeRiceSeasonCommandHandler(IUnitOfWork unitOfWork, ILogger<ChangeRiceSeasonCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(ChangeRiceSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var riceSeaons = await _unitOfWork.Repository<RiceVarietySeason>().ListAsync();
            foreach (var riceSeason in riceSeaons)
            {
                if (riceSeason.RiceVarietyId == request.RiceId &&
                    riceSeason.SeasonId == request.SeasonId)
                {
                    return Result<Guid>.Failure($"Already matching rice {request.RiceId} and season {request.SeasonId}");
                }
            }
            var newRiceSeason = new RiceVarietySeason
            {
                Id = Guid.NewGuid(),
                RiceVarietyId = request.RiceId,
                SeasonId = request.SeasonId
            };
            await _unitOfWork.Repository<RiceVarietySeason>().AddAsync(newRiceSeason);
            return await _unitOfWork.Repository<RiceVarietySeason>().SaveChangesAsync() == 1
                ? Result<Guid>.Success(newRiceSeason.Id, "Successfully matched rice with season.")
                : Result<Guid>.Failure("Failed to match rice with season.", "ChangeRiceSeasonFailed");
        }
        catch(Exception ex)
        {
         _logger.LogError(ex, "Error matching rice {RiceId} with season {SeasonId}", request.RiceId, request.SeasonId);

            return Result<Guid>.Failure("An error occurred during change rice season process.", "ChangeRiceSeasonFailed");   
        }
    }

}