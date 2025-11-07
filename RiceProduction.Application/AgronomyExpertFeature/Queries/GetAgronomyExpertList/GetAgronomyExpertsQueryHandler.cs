using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.AgronomyExpertResponses;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertList
{
    public class GetAgronomyExpertsQueryHandler : IRequestHandler<GetAgronomyExpertsQuery, PagedResult<List<AgronomyExpertResponse>>>
    {
        private readonly IAgronomyExpertRepository _expertRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAgronomyExpertsQueryHandler> _logger;

        public GetAgronomyExpertsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAgronomyExpertsQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _expertRepo = _unitOfWork.AgronomyExpertRepository;
            _logger = logger;
        }

        public async Task<PagedResult<List<AgronomyExpertResponse>>> Handle(GetAgronomyExpertsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var (result, totalCount) = await _expertRepo.GetAllAgronomyExpertAssignedOrNotByNameOrEmailAndPhoneNumberPagingAsync(
                    request.CurrentPage,
                    request.PageSize,
                    request.Search,
                    request.PhoneNumber,
                    request.FreeOrAssigned,
                    cancellationToken);

                var resultList = result.ToList();

                var agronomyExpertResponse = resultList.Select(expert => new AgronomyExpertResponse
                {
                    ExpertId = expert.Id,
                    ExpertName = expert.FullName,
                    ExpertPhoneNumber = expert.PhoneNumber,
                    Email = expert.Email,
                    ClusterId = expert.ClusterId,
                    AssignedDate = expert.AssignedDate
                }).ToList();

                return PagedResult<List<AgronomyExpertResponse>>.Success(
                    agronomyExpertResponse,
                    request.CurrentPage,
                    request.PageSize,
                    totalCount,
                    "Agronomy experts retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting agronomy experts");
                return PagedResult<List<AgronomyExpertResponse>>.Failure("An error occurred while processing your request");
            }
        }
    }
}
