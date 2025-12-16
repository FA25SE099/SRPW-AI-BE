using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.LateFarmerRecordFeature.Commands.CreateLateFarmerRecord;

public class CreateLateFarmerRecordCommandHandler : IRequestHandler<CreateLateFarmerRecordCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateLateFarmerRecordCommandHandler> _logger;

    public CreateLateFarmerRecordCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateLateFarmerRecordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateLateFarmerRecordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating late farmer record for cultivation task {TaskId}", request.CultivationTaskId);

            // 1. Get the cultivation task with all necessary navigation properties for validation
            var cultivationTask = await _unitOfWork.Repository<CultivationTask>()
                .GetQueryable()
                .Include(ct => ct.PlotCultivation)
                    .ThenInclude(pc => pc.Plot)
                .FirstOrDefaultAsync(ct => ct.Id == request.CultivationTaskId, cancellationToken);

            if (cultivationTask == null)
            {
                return Result<Guid>.Failure($"Cultivation task with ID {request.CultivationTaskId} not found");
            }

            if (cultivationTask.PlotCultivation == null)
            {
                return Result<Guid>.Failure($"Plot cultivation not found for cultivation task {request.CultivationTaskId}");
            }

            var plotCultivation = cultivationTask.PlotCultivation;
            
            if (plotCultivation.Plot == null)
            {
                return Result<Guid>.Failure($"Plot not found for plot cultivation {plotCultivation.Id}");
            }

            var plot = plotCultivation.Plot;

            // 2. Create the simplified late farmer record (only store FarmerId and CultivationTaskId)
            var lateFarmerRecord = new LateFarmerRecord
            {
                FarmerId = plot.FarmerId,
                CultivationTaskId = request.CultivationTaskId,
                RecordedAt = DateTime.UtcNow,
                Notes = request.Notes
            };

            // 3. Add and save
            await _unitOfWork.LateFarmerRecordRepository.AddAsync(lateFarmerRecord, cancellationToken);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Successfully created late farmer record {RecordId} for farmer {FarmerId} on task {TaskId}",
                lateFarmerRecord.Id, lateFarmerRecord.FarmerId, request.CultivationTaskId);

            return Result<Guid>.Success(
                lateFarmerRecord.Id,
                "Late farmer record created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating late farmer record for task {TaskId}", request.CultivationTaskId);
            return Result<Guid>.Failure("An error occurred while processing your request");
        }
    }
}
