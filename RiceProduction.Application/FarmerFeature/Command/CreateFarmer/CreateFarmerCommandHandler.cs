using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response.PlotResponse;
using RiceProduction.Application.FarmerFeature.Events;
using RiceProduction.Application.FarmerFeature.Events.SendEmailEvent;
using RiceProduction.Application.PlotFeature.Events;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;

namespace RiceProduction.Application.FarmerFeature.Command.CreateFarmer
{
    public class CreateFarmerCommandHandler : IRequestHandler<CreateFarmerCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateFarmerCommandHandler> _logger;
        private readonly IMediator _mediator;

        public CreateFarmerCommandHandler(
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork,
            ILogger<CreateFarmerCommandHandler> logger,
            IMediator mediator)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<Result<Guid>> Handle(CreateFarmerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.FullName))
                {
                    return Result<Guid>.Failure("Full name is required");
                }

                if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    return Result<Guid>.Failure("Phone number is required");
                }

                // Check if phone number already exists in Farmer table
                var existingFarmer = await _unitOfWork.FarmerRepository.GetFarmerByPhoneNumber(request.PhoneNumber, cancellationToken);
                if (existingFarmer != null)
                {
                    return Result<Guid>.Failure($"Phone number '{request.PhoneNumber}' already exists in the system");
                }

                // Check if phone number is already used as username in Identity
                var existingUser = await _userManager.FindByNameAsync(request.PhoneNumber);
                if (existingUser != null)
                {
                    return Result<Guid>.Failure($"Phone number '{request.PhoneNumber}' is already used as an account");
                }

                // Get ClusterId from ClusterManager if provided
                Guid? clusterId = null;
                if (request.ClusterManagerId.HasValue)
                {
                    var clusterManager = await _unitOfWork.ClusterManagerRepository
                        .GetClusterManagerByIdAsync(request.ClusterManagerId.Value, cancellationToken);
                    clusterId = clusterManager?.ClusterId;
                }

                // Create farmer
                const string TEMP_PASSWORD = "123456";
                
                var farmer = new Farmer
                {
                    UserName = request.PhoneNumber,
                    PhoneNumber = request.PhoneNumber,
                    FullName = request.FullName,
                    Address = request.Address,
                    FarmCode = request.FarmCode,
                    ClusterId = clusterId,
                    EmailConfirmed = true,
                    IsActive = true,
                };

                var createResult = await _userManager.CreateAsync(farmer, TEMP_PASSWORD);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create farmer: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to create farmer: {errors}");
                }

                // Assign role
                var roleResult = await _userManager.AddToRoleAsync(farmer, UserRole.Farmer.ToString());
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(farmer);
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign role to farmer: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to assign role: {errors}");
                }

                _logger.LogInformation("Created farmer with ID: {FarmerId}, Name: {FullName}", farmer.Id, farmer.FullName);

                // Create multiple plots if plot data is provided
                List<Guid> createdPlotIds = new();
                if (request.Plots != null && request.Plots.Any())
                {
                    foreach (var plotData in request.Plots)
                    {
                        if (plotData.PlotArea > 0)
                        {
                            var plot = new Plot
                            {
                                FarmerId = farmer.Id,
                                SoThua = plotData.SoThua,
                                SoTo = plotData.SoTo,
                                Area = plotData.PlotArea,
                                SoilType = plotData.SoilType,
                                Status = PlotStatus.PendingPolygon,
                                Boundary = null // Supervisor will assign polygon later
                            };

                            await _unitOfWork.Repository<Plot>().AddAsync(plot);
                            createdPlotIds.Add(plot.Id);
                        }
                    }

                    if (createdPlotIds.Any())
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Created {PlotCount} plot(s) for farmer: {FarmerId}", createdPlotIds.Count, farmer.Id);
                    }
                }

                // Publish PlotImportedEvent if plots were created
                if (createdPlotIds.Any() && request.ClusterManagerId.HasValue)
                {
                    // Get the created plots for the event
                    var createdPlots = await _unitOfWork.Repository<Plot>()
                        .ListAsync(p => createdPlotIds.Contains(p.Id));
                    
                    var plotResponses = createdPlots.Select(p => new PlotResponse
                    {
                        PlotId = p.Id,
                        SoThua = p.SoThua,
                        SoTo = p.SoTo,
                        Area = p.Area,
                        FarmerId = p.FarmerId,
                        FarmerName = farmer.FullName,
                        SoilType = p.SoilType,
                        Status = p.Status,
                        GroupId = p.GroupPlots.FirstOrDefault()?.GroupId
                    }).ToList();

                    await _mediator.Publish(new PlotImportedEvent
                    {
                        ImportedPlots = plotResponses,
                        ClusterManagerId = request.ClusterManagerId,
                        ImportedAt = DateTime.UtcNow,
                        TotalPlotsImported = plotResponses.Count
                    }, cancellationToken);                  

                    _logger.LogInformation("Published PlotImportedEvent for {PlotCount} plot(s) requiring polygon assignment", plotResponses.Count);

                    await _mediator.Publish(new FarmerCreatedEvent
                    {
                        FarmerId = farmer.Id,
                        FullName = farmer.FullName,
                        PhoneNumber = farmer.PhoneNumber,
                        Email = farmer.Email,
                        Password = TEMP_PASSWORD,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);

                    _logger.LogInformation("Published FarmerCreatedEvent for farmer {FarmerId} to send credentials email", farmer.Id);
                }

                return Result<Guid>.Success(farmer.Id);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating farmer");
                return Result<Guid>.Failure($"Error creating farmer: {ex.Message}");
            }
        }
    }
}

