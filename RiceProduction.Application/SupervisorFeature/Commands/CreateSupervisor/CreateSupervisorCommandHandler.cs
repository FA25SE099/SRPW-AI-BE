using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.FarmerFeature.Events.SendEmailEvent;
using RiceProduction.Application.SupervisorFeature.Events.SendEmailEventSupervisor;
using RiceProduction.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RiceProduction.Application.Common.Constants.ApplicationMessages;

namespace RiceProduction.Application.SupervisorFeature.Commands.CreateSupervisor
{
    public class CreateSupervisorCommandHandler : IRequestHandler<CreateSupervisorCommand, Result<Guid>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CreateSupervisorCommandHandler> _logger;
        private readonly IPublisher _publisher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        public CreateSupervisorCommandHandler(UserManager<ApplicationUser> userManager, ILogger<CreateSupervisorCommandHandler> logger, IPublisher publisher, IUnitOfWork unitOfWork, IMediator mediator)
        {
            _userManager = userManager;
            _logger = logger;
            _publisher = publisher;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
        }

        public async Task<Result<Guid>> Handle(CreateSupervisorCommand request, CancellationToken cancellationToken)
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
                // Create Supervisor user
                var supervisor = new RiceProduction.Domain.Entities.Supervisor
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true,
                    IsActive = true,
                    MaxFarmerCapacity = request.MaxFarmerCapacity
                };

                var psw = GenerateRandomPassword();
                var result = await _userManager.CreateAsync(supervisor, psw);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Result<Guid>.Failure($"Failed to create Supervisor user: {errors}");
                }
                await _userManager.AddToRoleAsync(supervisor, "Supervisor");
                _logger.LogInformation("Created Supervisor with ID: {SupervisorId}", supervisor.Id);
                await _mediator.Publish(new SupervisorCreatedEvent
                {
                    SupervisorId = supervisor.Id,
                    FullName = supervisor.FullName,
                    PhoneNumber = supervisor.PhoneNumber,
                    Email = supervisor.Email,
                    Password = psw,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                _logger.LogInformation("Published SupervisorCreatedEvent for supervisor {SupervisorId} to send credentials email", supervisor.Id);
                return Result<Guid>.Success(supervisor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating Supervisor");
                return Result<Guid>.Failure("An error occurred while creating the Supervisor");
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

