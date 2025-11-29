using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Commands.UpdatePestProtocol;

public class UpdatePestProtocolCommandHandler : IRequestHandler<UpdatePestProtocolCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _currentUser;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdatePestProtocolCommandHandler> _logger;

    public UpdatePestProtocolCommandHandler(
        IUnitOfWork unitOfWork,
        IUser currentUser,
        IMediator mediator,
        ILogger<UpdatePestProtocolCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdatePestProtocolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get existing pest protocol
            var pestProtocol = await _unitOfWork.Repository<PestProtocol>()
                .GetQueryable()
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (pestProtocol == null)
            {
                return Result<Guid>.Failure(
                    $"Pest Protocol with ID {request.Id} not found.",
                    "PestProtocolNotFound");
            }

            // 2. Check for duplicate name (excluding current protocol)
            var duplicateExists = await _unitOfWork.Repository<PestProtocol>()
                .ExistsAsync(p => p.Id != request.Id &&
                              p.Name.ToLower() == request.Name.ToLower());

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Pest Protocol with the name '{request.Name}' already exists.",
                    "DuplicatePestProtocolName");
            }

            // 3. Update properties
            pestProtocol.Name = request.Name.Trim();
            pestProtocol.Description = request.Description?.Trim();
            pestProtocol.Type = request.Type?.Trim();
            pestProtocol.ImageLinks = request.ImageLinks?.Where(link => !string.IsNullOrWhiteSpace(link))
                                                        .Select(link => link.Trim())
                                                        .ToList();
            pestProtocol.IsActive = request.IsActive;
            pestProtocol.Notes = request.Notes?.Trim();

            // 4. Update and save
            _unitOfWork.Repository<PestProtocol>().Update(pestProtocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully updated PestProtocol {ProtocolId} '{ProtocolName}' with {ImageCount} images",
                pestProtocol.Id, pestProtocol.Name, pestProtocol.ImageLinks?.Count ?? 0);

            return Result<Guid>.Success(
                pestProtocol.Id,
                $"Pest Protocol '{pestProtocol.Name}' updated successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database error while updating pest protocol {ProtocolId}",
                request.Id);

            return Result<Guid>.Failure(
                "A database error occurred while updating the pest protocol.",
                "DatabaseError");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating pest protocol {ProtocolId}",
                request.Id);

            return Result<Guid>.Failure(
                "An unexpected error occurred while updating the pest protocol.",
                "UpdatePestProtocolFailed");
        }
    }
}
