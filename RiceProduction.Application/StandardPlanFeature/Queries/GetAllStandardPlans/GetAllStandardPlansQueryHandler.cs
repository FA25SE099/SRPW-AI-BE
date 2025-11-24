using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;

namespace RiceProduction.Application.StandardPlanFeature.Queries.GetAllStandardPlans;

public class GetAllStandardPlansQueryHandler : IRequestHandler<GetAllStandardPlansQuery, Result<List<StandardPlanDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAllStandardPlansQueryHandler> _logger;

    public GetAllStandardPlansQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAllStandardPlansQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<List<StandardPlanDto>>> Handle(
        GetAllStandardPlansQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Getting all standard plans with filters: Category={CategoryId}, Search={SearchTerm}, IsActive={IsActive}",
                request.CategoryId, request.SearchTerm, request.IsActive);

            var query = _unitOfWork.Repository<StandardPlan>()
                .GetQueryable()
                .Where(sp => sp.GetType() == typeof(StandardPlan))
                .Include(sp => sp.Category)
                .Include(sp => sp.StandardPlanStages)
                .ThenInclude(sps => sps.StandardPlanTasks)
                .AsQueryable();

            if (request.CategoryId.HasValue)
            {
                query = query.Where(sp => sp.CategoryId == request.CategoryId.Value);
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(sp => sp.IsActive == request.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(sp =>
                    sp.PlanName.ToLower().Contains(searchTerm) ||
                    (sp.Description != null && sp.Description.ToLower().Contains(searchTerm)));
            }

            query = query.OrderByDescending(sp => sp.CreatedAt);

            var standardPlans = await query.ToListAsync(cancellationToken);

            var result = standardPlans.Select(sp => new StandardPlanDto
            {
                Id = sp.Id,
                Name = sp.PlanName,
                Description = sp.Description,
                CategoryId = sp.CategoryId,
                CategoryName = sp.Category.CategoryName,
                TotalDuration = sp.TotalDurationDays,
                IsActive = sp.IsActive,
                TotalTasks = sp.StandardPlanStages?
                    .SelectMany(sps => sps.StandardPlanTasks)
                    .Count() ?? 0,
                TotalStages = sp.StandardPlanStages?.Count ?? 0,
                CreatedAt = sp.CreatedAt,
                CreatedBy = sp.CreatedBy,
                LastModified = sp.LastModified,
                LastModifiedBy = sp.LastModifiedBy
            }).ToList();

            _logger.LogInformation("Successfully retrieved {Count} standard plans", result.Count);

            return Result<List<StandardPlanDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting standard plans");
            return Result<List<StandardPlanDto>>.Failure($"Failed to retrieve standard plans: {ex.Message}");
        }
    }
}