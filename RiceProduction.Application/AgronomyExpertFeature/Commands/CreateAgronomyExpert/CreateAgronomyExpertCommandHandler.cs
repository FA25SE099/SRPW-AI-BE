using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Application.AgronomyExpertFeature.Commands.CreateAgronomyExpert
{
    public class CreateAgronomyExpertCommandHandler : IRequestHandler<CreateAgronomyExpertCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateAgronomyExpertCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;

        public CreateAgronomyExpertCommandHandler(
            UserManager<ApplicationUser> userManager,
            ILogger<CreateAgronomyExpertCommandHandler> logger,
            IPublisher publisher,
            IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(CreateAgronomyExpertCommand request, CancellationToken cancellationToken)
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


                // Create Agronomy Expert user
                var expert = new AgronomyExpert
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true,
                    IsActive = true
                };
                var psw = "123456";
                var result = await _userManager.CreateAsync(expert, psw);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create Agronomy Expert: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to create Agronomy Expert: {errors}");
                }

                // Assign role
                var roleResult = await _userManager.AddToRoleAsync(expert, UserRole.AgronomyExpert.ToString());
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(expert);
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign role to Agronomy Expert: {Errors}", errors);
                    return Result<Guid>.Failure($"Failed to assign role: {errors}");
                }

                //// Publish user created event for SMS notification
                //await _publisher.Publish(new UserCreatedEvent
                //{
                //    PhoneNumber = expert.PhoneNumber!,
                //    Name = expert.FullName!
                //}, cancellationToken);

                _logger.LogInformation("Successfully created Agronomy Expert with ID: {ExpertId}", expert.Id);
                return Result<Guid>.Success(expert.Id, "Agronomy Expert created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Agronomy Expert");
                return Result<Guid>.Failure("An error occurred while creating Agronomy Expert");
            }
        }
    }
}
