using Microsoft.Extensions.Logging;
using AutoMapper;
using MediatR;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;
using RiceProduction.Domain.Entities;
using System.Linq.Expressions;
namespace RiceProduction.Application.RiceVarietyFeature.Queries.GetAllRiceVarieties;
public class GetAllRiceVarietiesQueryHandler : 
    IRequestHandler<GetAllRiceVarietiesQuery, Result<List<RiceVarietyResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetAllRiceVarietiesQueryHandler> _logger;

    public GetAllRiceVarietiesQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetAllRiceVarietiesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<List<RiceVarietyResponse>>> Handle(GetAllRiceVarietiesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Build the filter expression
            Expression<Func<RiceVariety, bool>> filter = v =>
                (string.IsNullOrEmpty(request.Search) || v.VarietyName.Contains(request.Search) || (v.Characteristics != null && v.Characteristics.Contains(request.Search))) &&
                (request.IsActive == null || v.IsActive == request.IsActive.Value);

            // Use ListAsync to retrieve all matching records (no paging)
            var riceVarieties = await _unitOfWork.Repository<RiceVariety>().ListAsync(
                filter: filter,
                orderBy: q => q.OrderBy(v => v.VarietyName));

            // Map the List<RiceVariety> to List<RiceVarietyResponse>
            var varietyResponses = riceVarieties
                .Select(v => new RiceVarietyResponse
                {
                    Id = v.Id,
                    VarietyName = v.VarietyName,
                    BaseGrowthDurationDays = v.BaseGrowthDurationDays,
                    BaseYieldPerHectare = v.BaseYieldPerHectare,
                    Description = v.Description,
                    Characteristics = v.Characteristics,
                    IsActive = v.IsActive
                    // Manually map any other properties
                })
                .ToList();

            return Result<List<RiceVarietyResponse>>
                .Success(varietyResponses, "Successfully retrieved all rice varieties.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all rice varieties (non-paged)");

            return Result<List<RiceVarietyResponse>>
                .Failure("An error occurred while retrieving rice varieties", "GetAllRiceVarieties failed.");
        }
    }
}