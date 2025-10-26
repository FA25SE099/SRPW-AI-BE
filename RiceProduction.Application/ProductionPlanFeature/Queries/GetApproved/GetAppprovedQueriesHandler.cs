using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApproved
{
    public class GetAppprovedQueriesHandler : IRequestHandler<GetApprovedQueries, Result<List<ProductionPlanDTO>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetAppprovedQueriesHandler> _logger;

        public GetAppprovedQueriesHandler(IUnitOfWork unitOfWork, IMapper mapper, ILogger<GetAppprovedQueriesHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<List<ProductionPlanDTO>>> Handle(GetApprovedQueries request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting to retrieve approved production plans with filters: GroupId={GroupId}, SupervisorId={SupervisorId}, FromDate={FromDate}, ToDate={ToDate}",
                     request.GroupId, request.SupervisorId, request.FromDate, request.ToDate);
                var query = _unitOfWork.Repository<ProductionPlan>().GetQueryable()
                     .Include(g => g.Group).ThenInclude(p => p.Plots)
                     .Include(g => g.Group).ThenInclude(r => r.RiceVariety)
                     .Include(g => g.Group).ThenInclude(c => c.Cluster)
                     .Include(g => g.Group).ThenInclude(s => s.Supervisor)
                     .Include(st => st.StandardPlan)
                     .Include(su => su.Submitter)
                     .Include(ap => ap.Approver)
                     .Include(s => s.CurrentProductionStages).ThenInclude(t => t.ProductionPlanTasks)
                     .Where(st => st.Status == RiceProduction.Domain.Enums.TaskStatus.Approved);

                var productionPlan = await query.OrderByDescending(PP => PP.ApprovedAt)
                                    .ThenBy(pp => pp.BasePlantingDate)
                                    .ToListAsync(cancellationToken);
                _logger.LogInformation("Found {Count} approved production plans", productionPlan.Count);

                if (!productionPlan.Any())
                {
                    _logger.LogWarning("No approved production plans found with the specified filters");
                    return Result<List<ProductionPlanDTO>>.Success(
                        new List<ProductionPlanDTO>(),
                        "No approved production plans found");
                }

                var productionPlanDTOs = _mapper.Map<List<ProductionPlanDTO>>(productionPlan);
                _logger.LogInformation("Successfully mapped {Count} production plans to DTOs", productionPlanDTOs.Count);
                return Result<List<ProductionPlanDTO>>.Success(
                   productionPlanDTOs,
                   $"Successfully retrieved {productionPlanDTOs.Count} approved production plans");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving approved production plans");
                return Result<List<ProductionPlanDTO>>.Failure(
                    $"Error retrieving approved production plans: {ex.Message}");
            }
        }
    }
}
