using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Commands.CreatePestProtocol;

public class CreatePestProtocolCommandHandler : IRequestHandler<CreatePestProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<CreatePestProtocolCommandHandler> _logger;

    public CreatePestProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<CreatePestProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreatePestProtocolCommand request, CancellationToken cancellationToken)
    {

        try
        {
            // 2. Check for duplicate pest protocol name
            var duplicateExists = await _unitOfWork.Repository<PestProtocol>()
                .ExistsAsync(p => p.Name.ToLower() == request.Name.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Pest Protocol with the name '{request.Name}' already exists.",
                    "DuplicatePestProtocolName");
            }

            // 3. Create the PestProtocol entity
            var pestProtocol = new PestProtocol
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Type = request.Type?.Trim(),
                ImageLink = request.ImageLink?.Trim(),
                IsActive = request.IsActive,
                Notes = request.Notes?.Trim()
            };

            // 4. Add and save
            await _unitOfWork.Repository<PestProtocol>().AddAsync(pestProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);


            _logger.LogInformation(
                "Successfully created PestProtocol {ProtocolId} '{ProtocolName}'",
                pestProtocol.Id, pestProtocol.Name);

            return Result<Guid>.Success(
                pestProtocol.Id,
                $"Pest Protocol '{pestProtocol.Name}' created successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while creating pest protocol '{ProtocolName}'",
                request.Name);

            return Result<Guid>.Failure(
                "A database error occurred while saving the pest protocol. Please check your data and try again.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while creating pest protocol '{ProtocolName}'}",
                request.Name);

            return Result<Guid>.Failure(
                "An unexpected error occurred while creating the pest protocol.",
                "CreatePestProtocolFailed");
        }
    }
}
