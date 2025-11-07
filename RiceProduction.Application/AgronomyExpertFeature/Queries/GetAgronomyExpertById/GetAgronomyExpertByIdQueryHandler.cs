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

namespace RiceProduction.Application.AgronomyExpertFeature.Queries.GetAgronomyExpertById
{
    public class GetAgronomyExpertByIdQueryHandler : IRequestHandler<GetAgronomyExpertByIdQuery, Result<AgronomyExpertResponse>>
    {
        private readonly IAgronomyExpertRepository _expertRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GetAgronomyExpertByIdQueryHandler> _logger;

        public GetAgronomyExpertByIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetAgronomyExpertByIdQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _expertRepo = _unitOfWork.AgronomyExpertRepository;
            _logger = logger;
        }

        public async Task<Result<AgronomyExpertResponse>> Handle(GetAgronomyExpertByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var expert = await _expertRepo.GetAgronomyExpertByIdAsync(
                    request.AgronomyExpertId,
                    cancellationToken);

                var agronomyExpertResponse = new AgronomyExpertResponse
                {
                    ExpertId = expert.Id,
                    ExpertName = expert.FullName,
                    ExpertPhoneNumber = expert.PhoneNumber,
                    Email = expert.Email,
                    ClusterId = expert.ClusterId,
                    AssignedDate = expert.AssignedDate
                };

                return Result<AgronomyExpertResponse>.Success(
                    agronomyExpertResponse,
                    "Agronomy experts retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting agronomy experts");
                return Result<AgronomyExpertResponse>.Failure("An error occurred while processing your request");
            }
        }
    }
}

