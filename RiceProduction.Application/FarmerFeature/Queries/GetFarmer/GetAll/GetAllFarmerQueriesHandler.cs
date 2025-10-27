using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.FarmerFeature.Queries.GetFarmer.GetAll
{
    public class GetAllFarmerQueriesHandler : IRequestHandler<GetAllFarmerQueries, PagedResult<IEnumerable<FarmerDTO>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetAllFarmerQueriesHandler> _logger;

        public GetAllFarmerQueriesHandler(IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetAllFarmerQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<PagedResult<IEnumerable<FarmerDTO>>> Handle(GetAllFarmerQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting all Page: {PageNumber}, PageSize:{PageSize}",
                    request.PageNumber, request.PageSize);

                Expression<Func<Farmer, bool>>? predicate = null;

                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    predicate = f => f.IsActive && (f.FullName.Contains(request.SearchTerm) || f.PhoneNumber != null && f.PhoneNumber.Contains(request.SearchTerm));
                }
                else
                {
                    predicate = f => f.IsActive;
                }

                var (items, totalCount) = await _unitOfWork.FarmerRepository
                    .GetPagedAsync(request.PageNumber, request.PageSize, predicate, cancellationToken);

                var farmerDTOs = _mapper.Map<IEnumerable<FarmerDTO>>(items);
                return PagedResult<IEnumerable<FarmerDTO>>.Success
                    (
                    data: farmerDTOs,
                    currentPage: request.PageNumber,
                    pageSize: request.PageSize,
                    totalCount: totalCount,
                    message: "Farmers retrieved successfully"
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured while getting all farmers");
                return PagedResult<IEnumerable<FarmerDTO>>.Failure(
                    error: ex.Message,
                    message: "Failed to retrieve farmers"
                    );

            }

        }
    }
}
