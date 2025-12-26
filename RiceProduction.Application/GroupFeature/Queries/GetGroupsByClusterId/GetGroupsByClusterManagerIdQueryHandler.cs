using NetTopologySuite.IO;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.GroupResponses;
using RiceProduction.Application.Common.Models.Response.SupervisorResponses;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.GroupFeature.Queries.GetGroupsByClusterId
{
    public class GetGroupsByClusterManagerIdQueryHandler : IRequestHandler<GetGroupsByClusterManagerIdQuery, PagedResult<List<GroupResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly WKTWriter _wktWriter;
        private readonly IUser _currentUser;

        public GetGroupsByClusterManagerIdQueryHandler(IUnitOfWork unitOfWork, IUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _wktWriter = new WKTWriter();
            _currentUser = currentUser;
        }

        public async Task<PagedResult<List<GroupResponse>>> Handle(GetGroupsByClusterManagerIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = (Guid)_currentUser.Id;
                if (userId == null || userId == Guid.Empty)
                {
                    return PagedResult<List<GroupResponse>>.Failure(
                        "Current user ID not found");
                }

                var clusterManager = await _unitOfWork.ClusterManagerRepository.GetClusterManagerByIdAsync(userId, cancellationToken);
                if (clusterManager == null)
                    return PagedResult<List<GroupResponse>>.Failure(
                                        $"Cluster Manager with ID {userId} not found");
                var groupListBelongToCluster = await _unitOfWork.Repository<Group>().ListAsync(g => g.ClusterId == clusterManager.ClusterId);
                var supervisorRepo = _unitOfWork.SupervisorRepository;
                var groupResponses = groupListBelongToCluster.Select(g => new GroupResponse
                {
                    ClusterId = g.ClusterId,
                    SupervisorId = g.SupervisorId,
                    SupervisorName = supervisorRepo.FindAsync(s => s.Id == g.SupervisorId).Result.FirstOrDefault().FullName ?? "No name",
                    RiceVarietyId = g.YearSeason?.RiceVarietyId,
                    SeasonId = g.YearSeason?.SeasonId,
                    PlantingDate = g.PlantingDate,
                    Status = g.Status,
                    IsException = g.IsException,
                    ExceptionReason = g.ExceptionReason,
                    ReadyForUavDate = g.ReadyForUavDate,
                    Area = g.Area != null ? _wktWriter.Write(g.Area) : null,
                    TotalArea = g.TotalArea
                }).ToList();
                groupResponses = groupResponses
                    .Skip((request.CurrentPage - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();
                return PagedResult<List<GroupResponse>>.Success(
                    groupResponses,
                    groupResponses.Count(),
                    request.CurrentPage,
                    request.PageSize);
            }
            catch (Exception ex)
            {
                return PagedResult<List<GroupResponse>>.Failure($"An error occurred while retrieving groups: {ex.Message}");
            }
        }
    }
}