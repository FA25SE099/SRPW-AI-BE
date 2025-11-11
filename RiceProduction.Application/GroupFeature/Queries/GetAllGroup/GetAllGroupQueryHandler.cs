using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.PlotFeature.Queries;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.GroupFeature.Queries.GetAllGroup
{
    public class GetAllGroupQueryHandler : IRequestHandler<GetAllGroupQuery, Result<IEnumerable<GroupDTO>>>
    {
        private readonly ILogger<GetAllGroupQueryHandler> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAllGroupQueryHandler(ILogger<GetAllGroupQueryHandler> logger, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<IEnumerable<GroupDTO>>> Handle(GetAllGroupQuery request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting all groups");

                var groups = await _unitOfWork.Repository<Group>().ListAsync(
                    orderBy: q => q.OrderBy(g => g.Area));

                if (!groups.Any())
                {
                    _logger.LogWarning("No groups found");
                    return Result<IEnumerable<GroupDTO>>.Success(
                        new List<GroupDTO>(),
                        "No groups found");
                }
                var groupDTOs = _mapper.Map<IEnumerable<GroupDTO>>(groups);

                _logger.LogInformation("Successfully retrieved {Count} groups", groups.Count);

                return Result<IEnumerable<GroupDTO>>.Success(
                    groupDTOs,
                    $"Successfully retrieved {groups.Count} groups");
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