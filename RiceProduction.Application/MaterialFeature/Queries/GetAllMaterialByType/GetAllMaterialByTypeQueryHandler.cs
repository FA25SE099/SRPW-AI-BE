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

namespace RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType
{
    public class GetAllMaterialByTypeQueryHandler : IRequestHandler<GetAllMaterialByTypeQuery, PagedResult<List<MaterialResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAllMaterialByTypeQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<List<MaterialResponse>>> Handle(GetAllMaterialByTypeQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var materialRepo = await _unitOfWork.Repository<Material>().ListAsync(
                    filter: m => m.IsActive && m.Type == request.MaterialType,
                    orderBy: q => q.OrderBy(m => m.Name));
                var materialPriceRepo = _unitOfWork.Repository<MaterialPrice>();
                // Filter for active materials of the requested type
                Expression<Func<Material, bool>> filter = m => m.IsActive && m.Type == request.MaterialType;

                // Get total count for pagination
                var totalCount = materialRepo.Count();

                // Apply paging in-memory (if your repo doesn't support skip/take)
                var pagedMaterials = materialRepo
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var materialResponses = pagedMaterials
                    .Select(m => new MaterialResponse
                    {
                        Name = m.Name,
                        Type = m.Type,
                        AmmountPerMaterial = m.AmmountPerMaterial,
                        Showout = m.AmmountPerMaterial.ToString() + m.Unit,
                        PricePerMaterial = materialPriceRepo.ListAsync(p=>p.MaterialId == m.Id && m.IsActive && p.ValidFrom<=DateTime.UtcNow).Result.FirstOrDefault().PricePerMaterial,
                        Unit = m.Unit,
                        Description = m.Description,
                        Manufacturer = m.Manufacturer,
                        IsActive = m.IsActive
                    })
                    .ToList();

                if (!materialResponses.Any())
                {
                    return PagedResult<List<MaterialResponse>>.Failure(
                        $"No materials found of type {request.MaterialType}");
                }

                return PagedResult<List<MaterialResponse>>.Success(
                    materialResponses,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    $"Successfully retrieved materials of type {request.MaterialType}");
            }
            catch (Exception ex)
            {
                return PagedResult<List<MaterialResponse>>.Failure(
                    $"Error retrieving materials: {ex.Message}");
            }
        }
    }
}
