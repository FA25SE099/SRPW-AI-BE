using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.ClusterManagerFeature.Events;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Events.SendEmailEvent;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RiceProduction.Application.Common.Constants.ApplicationMessages;

namespace RiceProduction.Application.ClusterManagerFeature.Commands.CreateClusterManager
{
    public class CreateClusterManagerCommandHandler : IRequestHandler<CreateClusterManagerCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateClusterManagerCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        public CreateClusterManagerCommandHandler(
            UserManager<ApplicationUser> userManager,
            ILogger<CreateClusterManagerCommandHandler> logger,
            IPublisher publisher,
            IUnitOfWork unitOfWork,
            IMediator mediator)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
        }

        public async Task<Result<Guid>> Handle(CreateClusterManagerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Check if user with email already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return Result<Guid>.Failure($"User with email '{request.Email}' already exists");
                }

                // Check if phone number already exists
                var existingPhone = _userManager.Users.Any(u => u.PhoneNumber == request.PhoneNumber);
                if (existingPhone)
                {
                    return Result<Guid>.Failure($"User with phone number '{request.PhoneNumber}' already exists");
                }

                // Create Cluster Manager user
                var clusterManager = new Domain.Entities.ClusterManager
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var psw = GenerateRandomPassword();
                var result = await _userManager.CreateAsync(clusterManager, psw);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create Cluster Manager: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to create Cluster Manager: {errors}");
                }

                // Assign role
                var roleResult = await _userManager.AddToRoleAsync(clusterManager, UserRole.ClusterManager.ToString());
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(clusterManager);
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign role to Cluster Manager: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to assign role: {errors}");
                }

                //// Publish user created event for SMS notification
                //await _publisher.Publish(new UserCreatedEvent
                //{
                //    PhoneNumber = clusterManager.PhoneNumber!,
                //    Name = clusterManager.FullName!
                //}, cancellationToken);

                _logger.LogInformation("Successfully created Cluster Manager with ID: {ManagerId}", clusterManager.Id);
                await _mediator.Publish(new ClusterManagerCreatedEvent
                {
                    ClusterManagerId = clusterManager.Id,
                    FullName = clusterManager.FullName,
                    PhoneNumber = clusterManager.PhoneNumber,
                    Email = clusterManager.Email,
                    Password = psw,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                _logger.LogInformation("Published ClusterManagerCreatedEvent for cluster manager {ClusterManagerId} to send credentials email", clusterManager.Id);
                return Result<Guid>.Success(clusterManager.Id, "Cluster Manager created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Cluster Manager");
                return Result<Guid>.Failure("An error occurred while creating Cluster Manager");
            }
        }
        private string GenerateRandomPassword()
        {
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%";

            var random = new Random();
            var password = new char[12];
            password[0] = upperChars[random.Next(upperChars.Length)];
            password[1] = lowerChars[random.Next(lowerChars.Length)];
            password[2] = digitChars[random.Next(digitChars.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];
            const string allChars = upperChars + lowerChars + digitChars + specialChars;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }
    }

}
