using RiceProduction.Application.Auth.Commands.Login;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;

public class GetAllMaterialByTypeQueryHandler : IRequestHandler<GetAllMaterialByTypeQuery, PagedResult<List<MaterialResponseForList>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllMaterialByTypeQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<List<MaterialResponseForList>>> Handle(GetAllMaterialByTypeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var materialRepo = await _unitOfWork.Repository<Material>().ListAsync(
                filter: m => m.IsActive && m.Type == request.MaterialType,
                orderBy: q => q.OrderBy(m => m.Name));
            var materialPriceRepoList = await _unitOfWork.Repository<MaterialPrice>().ListAsync();

            // Get total count for pagination
            var totalCount = materialRepo.Count();

            // Apply paging in-memory (if your repo doesn't support skip/take)
            var pagedMaterials = materialRepo
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Use provided DateTime or current DateTime
            var effectiveDate = request.PriceDateTime ?? DateTime.UtcNow;

            var materialResponses = pagedMaterials
                .Select(m => new MaterialResponseForList
                {
                    MaterialId = m.Id,
                    Name = m.Name,
                    Type = m.Type,
                    AmmountPerMaterial = m.AmmountPerMaterial,
                    Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                    PricePerMaterial = materialPriceRepoList
                    .Where(p => p.MaterialId == m.Id
                    && m.IsActive
                    && (p.ValidFrom <= effectiveDate)
                    && (!p.ValidTo.HasValue || (p.ValidTo.Value >= effectiveDate))
                         )
                    .OrderByDescending(p => p.ValidFrom)
                    .FirstOrDefault()?.PricePerMaterial ?? 0,
                    Unit = m.Unit,
                    Description = m.Description,
                    ImgUrls = m.imgUrls,
                    Manufacturer = m.Manufacturer,
                    IsActive = m.IsActive
                })
                .ToList();

            if (!materialResponses.Any())
            {
                return PagedResult<List<MaterialResponseForList>>.Failure(
                    $"No materials found of type {request.MaterialType}");
            }

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