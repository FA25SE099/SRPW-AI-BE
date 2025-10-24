using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Entities;

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
                //var supervisorList = await _unitOfWork.SupervisorRepository
                //    .GetAllSupervisorByNameOrEmailAndPhoneNumberPagingAsync(
                //    request.CurrentPage, request.PageSize, request.SearchNameOrEmail, request.SearchPhoneNumber, cancellationToken);
                
                var supervisorResponses = new List<SupervisorResponse>();
                var groupListBelongToCluster = await _unitOfWork.Repository<Group>().ListAsync(g => g.ClusterId == request.ClusterId);
                foreach (var group in groupListBelongToCluster)
                {
                    var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(group.Id);
                    if (supervisor == null)
                        return PagedResult<List<SupervisorResponse>>.Failure(
                                            $"Error retrieving supervisor list");
                    supervisorResponses.Add(new SupervisorResponse
                    {
                        SupervisorId = supervisor.Id,
                        FullName = supervisor.FullName,
                        Email = supervisor.Email,
                        PhoneNumber = supervisor.PhoneNumber,
                        Address = supervisor.Address,
                        CurrentFarmerCount = supervisor.CurrentFarmerCount,
                        LastActivityAt = supervisor.LastActivityAt
                    });
                }
                if (!supervisorResponses.Any())
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        $"No supervisors found");
                }
                supervisorResponses = supervisorResponses
                    .Where(s => !string.IsNullOrEmpty(request.SearchNameOrEmail) || 
                    ((s.FullName != null && s.FullName.Contains(request.SearchNameOrEmail)) ||
                    (s.Email != null && s.Email.Contains(request.SearchNameOrEmail))) 
                    && (!string.IsNullOrEmpty(s.PhoneNumber) || s.PhoneNumber.Contains(request.SearchPhoneNumber)))
                    .ToList();
                supervisorResponses = supervisorResponses
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();
                return PagedResult<List<SupervisorResponse>>.Success(
                    supervisorResponses,
                    supervisorResponses.Count(),
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
