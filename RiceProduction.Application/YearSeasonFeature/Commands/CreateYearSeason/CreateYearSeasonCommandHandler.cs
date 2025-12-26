using MediatR;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.YearSeasonFeature.Commands.CreateYearSeason;

public class CreateYearSeasonCommandHandler : IRequestHandler<CreateYearSeasonCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUser _user;
    private readonly ILogger<CreateYearSeasonCommandHandler> _logger;

    public CreateYearSeasonCommandHandler(
        IUnitOfWork unitOfWork,
        IUser user,
        ILogger<CreateYearSeasonCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateYearSeasonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var yearSeasonRepo = _unitOfWork.Repository<YearSeason>();

            var season = await _unitOfWork.Repository<Season>().GetEntityByIdAsync(request.SeasonId);
            if (season == null)
            {
                return Result<Guid>.Failure("Season not found");
            }

            var cluster = await _unitOfWork.Repository<Cluster>().GetEntityByIdAsync(request.ClusterId);
            if (cluster == null)
            {
                return Result<Guid>.Failure("Cluster not found");
            }

            RiceVariety? riceVariety = null;
            if (request.RiceVarietyId.HasValue)
            {
                riceVariety = await _unitOfWork.Repository<RiceVariety>().GetEntityByIdAsync(request.RiceVarietyId.Value);
                if (riceVariety == null)
                {
                    return Result<Guid>.Failure("Rice variety not found");
                }
            }
            else if (!request.AllowFarmerSelection)
            {
                return Result<Guid>.Failure("Rice variety must be provided when farmer selection is disabled");
            }

            var duplicate = await yearSeasonRepo.FindAsync(ys =>
                ys.ClusterId == request.ClusterId &&
                ys.SeasonId == request.SeasonId &&
                ys.Year == request.Year);

            if (duplicate != null)
            {
                return Result<Guid>.Failure($"YearSeason already exists for {season.SeasonName} {request.Year} in this cluster");
            }

            // Use Season dates as default if not provided
            DateTime startDate;
            DateTime endDate;
            
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                // Use provided dates
                startDate = request.StartDate.Value;
                endDate = request.EndDate.Value;
                
                _logger.LogInformation(
                    "Using provided dates: {StartDate} to {EndDate}",
                    startDate, endDate);
            }
            else
            {
                // Parse from Season's MM/DD format
                startDate = ParseSeasonDate(season.StartDate, request.Year);
                endDate = ParseSeasonDate(season.EndDate, request.Year);
                
                // Handle year wraparound (e.g., Winter-Spring: 11/01 to 04/30)
                if (endDate < startDate)
                {
                    endDate = endDate.AddYears(1);
                }
                
                _logger.LogInformation(
                    "Using default dates from Season {SeasonName}: {StartDate} to {EndDate}",
                    season.SeasonName, startDate, endDate);
            }

            var userId = _user.Id;
            Guid? expertId = null;
            if (userId.HasValue)
            {
                var expert = await _unitOfWork.AgronomyExpertRepository.GetAgronomyExpertByIdAsync(userId.Value);
                if (expert != null)
                {
                    expertId = expert.Id;
                }
            }

            var id = await yearSeasonRepo.GenerateNewGuid(Guid.NewGuid());
            var newYearSeason = new YearSeason
            {
                Id = id,
                SeasonId = request.SeasonId,
                ClusterId = request.ClusterId,
                Year = request.Year,
                RiceVarietyId = request.RiceVarietyId,
                ManagedByExpertId = expertId,
                StartDate = startDate,
                EndDate = endDate,
                BreakStartDate = request.BreakStartDate,
                BreakEndDate = request.BreakEndDate,
                PlanningWindowStart = request.PlanningWindowStart,
                PlanningWindowEnd = request.PlanningWindowEnd,
                AllowFarmerSelection = request.AllowFarmerSelection,
                FarmerSelectionWindowStart = request.FarmerSelectionWindowStart,
                FarmerSelectionWindowEnd = request.FarmerSelectionWindowEnd,
                Status = SeasonStatus.Draft,
                Notes = request.Notes
            };

            await yearSeasonRepo.AddAsync(newYearSeason);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Created YearSeason with ID: {YearSeasonId} for Cluster: {ClusterId}, Season: {SeasonId}, Year: {Year}",
                id, request.ClusterId, request.SeasonId, request.Year);

            return Result<Guid>.Success(id, "YearSeason created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating YearSeason");
            return Result<Guid>.Failure("Failed to create YearSeason");
        }
    }
    
    /// <summary>
    /// Parse season date from MM/DD format to actual DateTime
    /// </summary>
    private static DateTime ParseSeasonDate(string mmddString, int year)
    {
        var parts = mmddString.Split('/');
        var month = int.Parse(parts[0]);
        var day = int.Parse(parts[1]);
        return new DateTime(year, month, day);
    }
}

