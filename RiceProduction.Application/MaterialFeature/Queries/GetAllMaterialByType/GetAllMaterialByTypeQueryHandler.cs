using Microsoft.EntityFrameworkCore;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;

namespace RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;

public class GetAllMaterialByTypeQueryHandler : IRequestHandler<GetAllMaterialByTypeQuery, PagedResult<List<MaterialResponseForList>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllMaterialByTypeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<List<MaterialResponseForList>>> Handle(GetAllMaterialByTypeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var materialsQuery = _context.Materials
                .AsNoTracking()
                .Where(m => m.IsActive && m.Type == request.MaterialType);

            var totalCount = await materialsQuery.CountAsync(cancellationToken);

            var pagedMaterials = await materialsQuery
                .OrderBy(m => m.Name)
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            if (!pagedMaterials.Any())
            {
                return PagedResult<List<MaterialResponseForList>>.Failure(
                    $"No materials found of type {request.MaterialType}");
            }

            var materialIds = pagedMaterials.Select(m => m.Id).ToList();
            var materialPrices = await _context.MaterialPrices
                .AsNoTracking()
                .Where(p => materialIds.Contains(p.MaterialId))
                .ToListAsync(cancellationToken);

            var effectiveDate = request.PriceDateTime ?? DateTime.UtcNow;

            var materialResponses = pagedMaterials
                .Select(m => new MaterialResponseForList
                {
                    MaterialId = m.Id,
                    Name = m.Name,
                    Type = m.Type,
                    AmmountPerMaterial = m.AmmountPerMaterial,
                    Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                    PricePerMaterial = materialPrices
                        .Where(p => p.MaterialId == m.Id
                            && p.ValidFrom <= effectiveDate
                            && (!p.ValidTo.HasValue || p.ValidTo.Value >= effectiveDate))
                        .OrderByDescending(p => p.ValidFrom)
                        .FirstOrDefault()?.PricePerMaterial ?? 0,
                    Unit = m.Unit,
                    Description = m.Description,
                    ImgUrls = m.imgUrls,
                    Manufacturer = m.Manufacturer,
                    IsActive = m.IsActive,
                    IsPartition = m.IsPartition
                })
                .ToList();

            var message = request.PriceDateTime.HasValue
                ? $"Successfully retrieved materials of type {request.MaterialType} with prices as of {request.PriceDateTime.Value:yyyy-MM-dd HH:mm:ss}"
                : $"Successfully retrieved materials of type {request.MaterialType}";

            return PagedResult<List<MaterialResponseForList>>.Success(
                materialResponses,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                message);
        }
        catch (Exception ex)
        {
            return PagedResult<List<MaterialResponseForList>>.Failure(
                $"Error retrieving materials: {ex.Message}");
        }
    }
}