using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.WeatherProtocolFeature.Commands.UpdateWeatherProtocol;

public class UpdateWeatherProtocolCommandHandler : IRequestHandler<UpdateWeatherProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateWeatherProtocolCommandHandler> _logger;

    public UpdateWeatherProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<UpdateWeatherProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateWeatherProtocolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get existing weather protocol
            var weatherProtocol = await _unitOfWork.Repository<WeatherProtocol>()
                .GetQueryable()
                .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

            if (weatherProtocol == null)
            {
                return Result<Guid>.Failure(
                    $"Weather Protocol with ID {request.Id} not found.",
                    "WeatherProtocolNotFound");
            }

            // 2. Check for duplicate name (excluding current protocol)
            var duplicateExists = await _unitOfWork.Repository<WeatherProtocol>()
                .ExistsAsync(w => w.Id != request.Id &&
                              w.Name.ToLower() == request.Name.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Weather Protocol with the name '{request.Name}' already exists.",
                    "DuplicateWeatherProtocolName");
            }

            // 3. Update properties
            weatherProtocol.Name = request.Name.Trim();
            weatherProtocol.Description = request.Description?.Trim();
            weatherProtocol.Source = request.Source?.Trim();
            weatherProtocol.SourceLink = request.SourceLink?.Trim();
            weatherProtocol.ImageLinks = request.ImageLinks?.Where(link => !string.IsNullOrWhiteSpace(link))
                                                           .Select(link => link.Trim())
                                                           .ToList();
            weatherProtocol.IsActive = request.IsActive;
            weatherProtocol.Notes = request.Notes?.Trim();

            // 4. Update and save
            _unitOfWork.Repository<WeatherProtocol>().Update(weatherProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully updated WeatherProtocol {ProtocolId} '{ProtocolName}' with {ImageCount} images",
                weatherProtocol.Id, weatherProtocol.Name, weatherProtocol.ImageLinks?.Count ?? 0);

            return Result<Guid>.Success(
                weatherProtocol.Id,
                $"Weather Protocol '{weatherProtocol.Name}' updated successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while updating weather protocol {ProtocolId}",
                request.Id);

            return Result<Guid>.Failure(
                "A database error occurred while updating the weather protocol.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating weather protocol {ProtocolId}",
                request.Id);

            return Result<Guid>.Failure(
                "An unexpected error occurred while updating the weather protocol.",
                "UpdateWeatherProtocolFailed");
        }
    }
}
