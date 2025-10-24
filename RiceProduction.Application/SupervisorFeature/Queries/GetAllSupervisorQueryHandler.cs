using Microsoft.AspNetCore.Identity;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.MaterialResponses;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Application.MaterialFeature.Queries.GetAllMaterialByType;
using RiceProduction.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.SupervisorFeature.Queries
{
    public class GetAllSupervisorQueryHandler : IRequestHandler<GetAllSupervisorQuery, PagedResult<List<SupervisorResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdentityService _identityService;

        public GetAllSupervisorQueryHandler(IUnitOfWork unitOfWork, IIdentityService identityService)
        {
            _unitOfWork = unitOfWork;
            _identityService = identityService;
        }

        public async Task<PagedResult<List<SupervisorResponse>>> Handle(GetAllSupervisorQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var supervisorRepo = await _unitOfWork.SupervisorRepository
                    .GetAllSupervisorByNameOrEmailAndPhoneNumberPagingAsync(
                    );
                // Get total count for pagination
                var totalCount = supervisorRepo.Count();
                // Apply paging in-memory (if your repo doesn't support skip/take)
                var pagedSupervisors = supervisorRepo
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();
                var supervisorResponses = new List<SupervisorResponse>();
                foreach (var supervisor in pagedSupervisors)
                {
                    var user = await _identityService.GetUserAsync(supervisor.UserId.ToString());
                    supervisorResponses.Add(new SupervisorResponse
                    {
                        SupervisorId = supervisor.Id,
                        FullName = supervisor.Name,
                        Email = user?.Email,
                        PhoneNumber = supervisor.PhoneNumber,
                        Address = supervisor.Address,
                        CurrentFarmerCount = supervisor.CurrentFarmerCount,
                        LastActivityAt = supervisor.La
                    });
                }
                return PagedResult<List<SupervisorResponse>>.Success(
                    supervisorResponses,
                    totalCount,
                    request.CurrentPage,
                    request.PageSize);
            }
            catch (Exception ex)
            {
                return PagedResult<List<SupervisorResponse>>.Failure(
                    $"Error retrieving supervisor list: {ex.Message}");
            }
        }
    }
}
