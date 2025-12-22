using System.Linq.Expressions;
using AutoMapper;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.GroupFeature.Queries.GetAllGroup
{
    public class GetAllGroupQueryHandler
        : IRequestHandler<GetAllGroupQuery, Result<IEnumerable<GroupDTO>>>
    {
        private readonly ILogger<GetAllGroupQueryHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllGroupQueryHandler(
            ILogger<GetAllGroupQueryHandler> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<IEnumerable<GroupDTO>>> Handle(
            GetAllGroupQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Getting groups with filters: ClusterId={ClusterId}, SeasonId={SeasonId}",
                    request.ClusterId, request.SeasonId);
                var groupsQuery = _unitOfWork.Repository<Group>()
                    .GetQueryable()
                    .Include(g => g.YearSeason)
                    .Where(g => (request.ClusterId == Guid.Empty || g.ClusterId == request.ClusterId) &&
                               (request.SeasonId == Guid.Empty || (g.YearSeason != null && g.YearSeason.SeasonId == request.SeasonId)) &&
                               g.Status == GroupStatus.Active)
                    .OrderBy(g => g.Area);

                var groups = await groupsQuery.ToListAsync(cancellationToken);

                if (!groups.Any())
                {
                    _logger.LogWarning(
                        "No groups found for ClusterId={ClusterId}, SeasonId={SeasonId}",
                        request.ClusterId, request.SeasonId);

                    return Result<IEnumerable<GroupDTO>>.Success(
                        new List<GroupDTO>(),
                        "No groups found");
                }

                var groupDTOs = _mapper.Map<IEnumerable<GroupDTO>>(groups);

                _logger.LogInformation(
                    "Successfully retrieved {Count} groups",
                    groups.Count());

                return Result<IEnumerable<GroupDTO>>.Success(
                    groupDTOs,
                    $"Successfully retrieved {groups.Count()} groups");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting all groups");
                return Result<IEnumerable<GroupDTO>>.Failure(
                    $"Error retrieving groups: {ex.Message}");
            }
        }
    }
}
