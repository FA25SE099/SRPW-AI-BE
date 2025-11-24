using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.WeatherProtocolFeature.Commands.CreateWeatherProtocol;

public class CreateWeatherProtocolCommandHandler : IRequestHandler<CreateWeatherProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateWeatherProtocolCommandHandler> _logger;

    public CreateWeatherProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<CreateWeatherProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateWeatherProtocolCommand request, CancellationToken cancellationToken)
    {

        try
        {
            // 2. Check for duplicate weather protocol name
            var duplicateExists = await _unitOfWork.Repository<WeatherProtocol>()
                .ExistsAsync(w => w.Name.ToLower() == request.Name.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Weather Protocol with the name '{request.Name}' already exists.",
                    "DuplicateWeatherProtocolName");
            }

            // 3. Create the WeatherProtocol entity
            var weatherProtocol = new WeatherProtocol
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Source = request.Source?.Trim(),
                SourceLink = request.SourceLink?.Trim(),
                ImageLink = request.ImageLink?.Trim(),
                IsActive = request.IsActive,
                Notes = request.Notes?.Trim()
            };

            // 4. Add and save
            await _unitOfWork.Repository<WeatherProtocol>().AddAsync(weatherProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);


            _logger.LogInformation(
                "Successfully created WeatherProtocol {ProtocolId} '{ProtocolName}'",
                weatherProtocol.Id, weatherProtocol.Name);

            return Result<Guid>.Success(
                weatherProtocol.Id,
                $"Weather Protocol '{weatherProtocol.Name}' created successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while creating weather protocol '{ProtocolName}'",
                request.Name);

            return Result<Guid>.Failure(
                "A database error occurred while saving the weather protocol. Please check your data and try again.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating weather protocol '{ProtocolName}'",
                request.Name);

            return Result<Guid>.Failure(
                "An unexpected error occurred while creating the weather protocol.",
                "CreateWeatherProtocolFailed");
        }
    }
}