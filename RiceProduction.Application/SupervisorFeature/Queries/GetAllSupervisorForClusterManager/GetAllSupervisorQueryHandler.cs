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

namespace RiceProduction.Application.SupervisorFeature.Queries.GetAllSupervisorForClusterManager
{
    public class GetAllSupervisorQueryHandler : IRequestHandler<GetAllSupervisorQuery, PagedResult<List<SupervisorResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdentityService _identityService;
        private readonly IUser _currentUser;

        public GetAllSupervisorQueryHandler(IUnitOfWork unitOfWork, IIdentityService identityService, IUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _identityService = identityService;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<List<SupervisorResponse>>> Handle(GetAllSupervisorQuery request, CancellationToken cancellationToken)
        {
            try 
            {
                var userId = (Guid) _currentUser.Id;
                if(userId == null || userId == Guid.Empty)
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        "Current user ID not found");
                }

                var supervisorResponses = new List<SupervisorResponse>();
                var clusterManager = await _unitOfWork.ClusterManagerRepository.GetClusterManagerByIdAsync(userId, cancellationToken);
                if (clusterManager == null)
                    return PagedResult<List<SupervisorResponse>>.Failure(
                                        $"Cluster Manager with ID {userId} not found");
                var groupListBelongToCluster = await _unitOfWork.Repository<Group>().ListAsync(g => g.ClusterId == clusterManager.ClusterId);
                foreach (var group in groupListBelongToCluster)
                {
                    Guid supId;
                    if(group.SupervisorId == null)
                        continue;
                    else 
                        supId = group.SupervisorId.Value;
                    var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(supId);
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
                        LastActivityAt = supervisor.LastActivityAt,
                        ClusterName = clusterManager.ManagedCluster?.ClusterName ?? "N/A",
                        ClusterId = supervisor.ManagedCluster?.Id
                    });
                }
                if (!supervisorResponses.Any())
                {
                    return PagedResult<List<SupervisorResponse>>.Failure(
                        $"No supervisors found with {userId} cluster id = {clusterManager.ClusterId}");
                }
                if (!string.IsNullOrEmpty(request.SearchNameOrEmail) || !string.IsNullOrEmpty(request.SearchPhoneNumber))
                {
                    supervisorResponses = supervisorResponses
                        .Where(s =>
                        {
                            // Name or Email filter
                            bool nameOrEmailMatch = true; // Default to true if no filter
                            if (!string.IsNullOrEmpty(request.SearchNameOrEmail))
                            {
                                nameOrEmailMatch =
                                    s.FullName != null && s.FullName.Contains(request.SearchNameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                                    s.Email != null && s.Email.Contains(request.SearchNameOrEmail, StringComparison.OrdinalIgnoreCase);
                            }

                            // Phone number filter
                            bool phoneMatch = true; // Default to true if no filter
                            if (!string.IsNullOrEmpty(request.SearchPhoneNumber))
                            {
                                phoneMatch = s.PhoneNumber != null && s.PhoneNumber.Contains(request.SearchPhoneNumber);
                            }

                            // Both conditions must be true (AND logic)
                            return nameOrEmailMatch && phoneMatch;
                        })
                        .ToList();
                }
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
