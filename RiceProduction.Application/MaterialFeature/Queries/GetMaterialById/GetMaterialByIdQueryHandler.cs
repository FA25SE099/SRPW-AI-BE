using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.GetMaterialById;

public class GetMaterialByIdQueryHandler : IRequestHandler<GetMaterialByIdQuery, Result<MaterialResponseForList>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetMaterialByIdQueryHandler> _logger;

    public GetMaterialByIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetMaterialByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<MaterialResponseForList>> Handle(GetMaterialByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var material = await _unitOfWork.Repository<Material>().FindAsync(
                match: m => m.Id == request.MaterialId && m.IsActive
            );

            if (material == null)
            {
                return Result<MaterialResponseForList>.Failure($"Material with ID {request.MaterialId} not found or is inactive.", "MaterialNotFound");
            }

            var currentDate = DateTime.Now;
            var materialPrices = await _unitOfWork.Repository<MaterialPrice>().ListAsync(
                filter: p => p.MaterialId == request.MaterialId &&
                           (p.ValidFrom.CompareTo(currentDate) <= 0) &&
                           (!p.ValidTo.HasValue || (p.ValidTo.Value.Date.CompareTo(currentDate) >= 0))
            );

            var currentPrice = materialPrices
                .OrderByDescending(p => p.ValidFrom)
                .FirstOrDefault()?.PricePerMaterial ?? 0;

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
                IsActive = material.IsActive
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
