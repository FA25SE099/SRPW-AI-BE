using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.ExpertPlanResponses;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.ProductionPlanFeature.Queries.GetApprovedPlan;

public class GetApprovedPlansQueryHandler : 
    IRequestHandler<GetApprovedPlansQuery, PagedResult<List<ExpertPendingPlanItemResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetApprovedPlansQueryHandler> _logger;

    public GetApprovedPlansQueryHandler(IUnitOfWork unitOfWork, ILogger<GetApprovedPlansQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<List<ExpertPendingPlanItemResponse>>> Handle(GetApprovedPlansQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var planRepo = _unitOfWork.Repository<ProductionPlan>();

            // 1. Xây dựng biểu thức lọc động (không áp dụng paging ở bước này)
            Expression<Func<ProductionPlan, bool>> filter = p =>
                p.Status == RiceProduction.Domain.Enums.TaskStatus.Approved &&
                (!request.Year.HasValue || p.BasePlantingDate.Year == request.Year.Value) &&
                (!request.SeasonId.HasValue || (p.GroupId.HasValue && p.Group != null && p.Group.YearSeason != null && p.Group.YearSeason.SeasonId == request.SeasonId.Value));
            
            // Tải toàn bộ Plans phù hợp với filter
            var allPlans = await planRepo.ListAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(p => p.ApprovedAt),
                includeProperties: q => q
                    .Include(p => p.Group)
                        .ThenInclude(g => g.YearSeason) 
                    .Include(p => p.Submitter)
            );

            // 2. Lấy TotalCount
            var totalCount = allPlans.Count;
            
            // 3. Áp dụng phân trang (in-memory paging)
            var pagedPlans = allPlans
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // 4. Ánh xạ dữ liệu
            var responseData = pagedPlans.Select(p => new ExpertPendingPlanItemResponse
            {
                Id = p.Id,
                PlanName = p.PlanName,
                GroupId = p.GroupId,
                GroupArea = p.TotalArea.HasValue ? $"{p.TotalArea.Value} ha" : "N/A",
                BasePlantingDate = p.BasePlantingDate,
                Status = p.Status,
                SubmittedAt = p.SubmittedAt,
                SubmitterName = p.Submitter != null ? p.Submitter.FullName : "Unknown"
            }).ToList();

            return PagedResult<List<ExpertPendingPlanItemResponse>>.Success(
                responseData,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                "Successfully retrieved approved plans.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approved plans.");
            return PagedResult<List<ExpertPendingPlanItemResponse>>.Failure("Failed to retrieve approved plans.", "GetApprovedPlansFailed");
        }
    }
}