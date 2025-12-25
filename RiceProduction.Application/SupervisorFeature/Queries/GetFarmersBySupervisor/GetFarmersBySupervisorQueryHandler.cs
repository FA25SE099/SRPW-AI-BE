using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Repository;

namespace RiceProduction.Application.SupervisorFeature.Queries.GetFarmersBySupervisor;

public class GetFarmersBySupervisorQueryHandler : IRequestHandler<GetFarmersBySupervisorQuery, PagedResult<List<FarmerDTO>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetFarmersBySupervisorQueryHandler> _logger;

    public GetFarmersBySupervisorQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetFarmersBySupervisorQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<List<FarmerDTO>>> Handle(
        GetFarmersBySupervisorQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify supervisor exists using SupervisorRepository
            var supervisor = await _unitOfWork.SupervisorRepository.GetSupervisorByIdAsync(request.SupervisorId, cancellationToken);

            if (supervisor == null)
            {
                return PagedResult<List<FarmerDTO>>.Failure(
                    "Supervisor not found.",
                    "NotFound");
            }

            IReadOnlyList<Farmer> farmers;

            if (request.OnlyAssigned)
            {
                // Get farmers from plots in groups supervised by this supervisor
                // Supervisor -> Groups -> GroupPlots -> Plots -> Farmers
                var groups = await _unitOfWork.Repository<Group>()
                    .ListAsync(
                        filter: g => g.SupervisorId == request.SupervisorId,
                        includeProperties: q => q.Include(g => g.GroupPlots)
                            .ThenInclude(gp => gp.Plot)
                                .ThenInclude(p => p.Farmer)
                    );

                // Extract unique farmers from all groups
                var farmerIds = groups
                    .SelectMany(g => g.GroupPlots)
                    .Select(gp => gp.Plot.FarmerId)
                    .Distinct()
                    .ToList();

                if (!farmerIds.Any())
                {
                    return PagedResult<List<FarmerDTO>>.Success(
                        new List<FarmerDTO>(),
                        request.CurrentPage,
                        request.PageSize,
                        0,
                        "No farmers found in supervised groups.");
                }

                farmers = await _unitOfWork.FarmerRepository.ListAsync(
                    filter: f => farmerIds.Contains(f.Id) && f.IsActive &&
                        (!string.IsNullOrWhiteSpace(request.SearchTerm)
                            ? f.FullName.Contains(request.SearchTerm) ||
                              (f.PhoneNumber != null && f.PhoneNumber.Contains(request.SearchTerm))
                            : true),
                    includeProperties: q => q.Include(f => f.OwnedPlots)
                );
            }
            else
            {
                // Get all farmers in the supervisor's cluster
                if (!supervisor.ClusterId.HasValue)
                {
                    return PagedResult<List<FarmerDTO>>.Success(
                        new List<FarmerDTO>(),
                        request.CurrentPage,
                        request.PageSize,
                        0,
                        "Supervisor is not assigned to any cluster.");
                }

                farmers = await _unitOfWork.FarmerRepository.ListAsync(
                    filter: f => f.ClusterId == supervisor.ClusterId && f.IsActive &&
                        (!string.IsNullOrWhiteSpace(request.SearchTerm)
                            ? f.FullName.Contains(request.SearchTerm) ||
                              (f.PhoneNumber != null && f.PhoneNumber.Contains(request.SearchTerm))
                            : true),
                    includeProperties: q => q.Include(f => f.OwnedPlots)
                );
            }

            var totalCount = farmers.Count;

            // Apply pagination
            var pagedFarmers = farmers
                .Skip((request.CurrentPage - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Map to DTO
            var farmerDTOs = pagedFarmers.Select(f => new FarmerDTO
            {
                FarmerId = f.Id,
                FullName = f.FullName,
                Address = f.Address,
                PhoneNumber = f.PhoneNumber,
                IsActive = f.IsActive,
                IsVerified = f.EmailConfirmed,
                LastActivityAt = f.LastActivityAt,
                FarmCode = f.FarmCode,
                PlotCount = f.OwnedPlots?.Count ?? 0
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} farmers for supervisor {SupervisorId} (OnlyAssigned: {OnlyAssigned})",
                farmerDTOs.Count,
                request.SupervisorId,
                request.OnlyAssigned);

            return PagedResult<List<FarmerDTO>>.Success(
                farmerDTOs,
                request.CurrentPage,
                request.PageSize,
                totalCount,
                $"Successfully retrieved {(request.OnlyAssigned ? "assigned" : "all")} farmers.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving farmers for supervisor {SupervisorId}",
                request.SupervisorId);
            return PagedResult<List<FarmerDTO>>.Failure(
                "An error occurred while retrieving farmers.",
                "GetFarmersFailed");
        }
    }
}
