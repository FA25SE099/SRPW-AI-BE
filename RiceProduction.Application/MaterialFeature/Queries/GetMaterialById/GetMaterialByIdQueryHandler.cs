using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;

namespace RiceProduction.Application.MaterialFeature.Queries.GetMaterialById;

public class GetMaterialByIdQueryHandler : IRequestHandler<GetMaterialByIdQuery, Result<MaterialResponseForList>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetMaterialByIdQueryHandler> _logger;

    public GetMaterialByIdQueryHandler(IApplicationDbContext context, ILogger<GetMaterialByIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<MaterialResponseForList>> Handle(GetMaterialByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var material = await _context.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.MaterialId && m.IsActive, cancellationToken);

            if (material == null)
            {
                return Result<MaterialResponseForList>.Failure($"Material with ID {request.MaterialId} not found or is inactive.", "MaterialNotFound");
            }

            var currentDate = DateTime.UtcNow;
            var currentPrice = await _context.MaterialPrices
                .AsNoTracking()
                .Where(p => p.MaterialId == request.MaterialId &&
                            p.ValidFrom.CompareTo(currentDate) <= 0 &&
                            (!p.ValidTo.HasValue || p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))
                .OrderByDescending(p => p.ValidFrom)
                .Select(p => (decimal?)p.PricePerMaterial)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            var response = new MaterialResponseForList
            {
                MaterialId = material.Id,
                Name = material.Name,
                Type = material.Type,
                AmmountPerMaterial = material.AmmountPerMaterial,
                Showout = material.AmmountPerMaterial.ToString() + material.Unit,
                PricePerMaterial = currentPrice,
                Unit = material.Unit,
                Description = material.Description,
                ImgUrls = material.imgUrls,
                Manufacturer = material.Manufacturer,
                IsActive = material.IsActive,
                IsPartition = material.IsPartition
            };

            _logger.LogInformation("Successfully retrieved material with ID: {MaterialId}", request.MaterialId);
            return Result<MaterialResponseForList>.Success(response, "Material retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving material with ID: {MaterialId}", request.MaterialId);
            return Result<MaterialResponseForList>.Failure("An error occurred while retrieving the material.", "RetrievalFailed");
        }
    }
}
