using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Application.PestProtocolFeature.Commands.CreatePestProtocol;

public class CreatePestProtocolCommandHandler : IRequestHandler<CreatePestProtocolCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreatePestProtocolCommandHandler> _logger;

    public CreatePestProtocolCommandHandler(
        IApplicationDbContext context,
        ILogger<CreatePestProtocolCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreatePestProtocolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Check for duplicate pest protocol name
            var normalizedName = request.Name.Trim().ToLower();
            var duplicateExists = await _context.PestProtocols
                .AsNoTracking()
                .AnyAsync(p => p.Name.ToLower() == normalizedName, cancellationToken);

            if (duplicateExists)
            {
                return Result<Guid>.Failure(
                    $"A Pest Protocol with the name '{request.Name}' already exists.",
                    "DuplicatePestProtocolName");
            }

            // 2. Create the PestProtocol entity
            var pestProtocol = new PestProtocol
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Type = request.Type?.Trim(),
                ImageLinks = request.ImageLinks?.Where(link => !string.IsNullOrWhiteSpace(link))
                                               .Select(link => link.Trim())
                                               .ToList(),
                IsActive = request.IsActive,
                Notes = request.Notes?.Trim()
            };

            // 3. Add and save
            _context.PestProtocols.Add(pestProtocol);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully created PestProtocol {ProtocolId} '{ProtocolName}' with {ImageCount} images",
                pestProtocol.Id, pestProtocol.Name, pestProtocol.ImageLinks?.Count ?? 0);

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
                "Unexpected error while creating pest protocol '{ProtocolName}'",
                request.Name);

            return Result<Guid>.Failure(
                "An unexpected error occurred while creating the pest protocol.",
                "CreatePestProtocolFailed");
        }
    }
}
