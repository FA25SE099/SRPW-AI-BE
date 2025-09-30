using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Data
{
    public class ApplicationDbContextInitialiser
    {
        private readonly ILogger<ApplicationDbContextInitialiser> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public ApplicationDbContextInitialiser(
            ILogger<ApplicationDbContextInitialiser> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task InitialiseAsync()
        {
            try
            {
                // See https://jasontaylor.dev/ef-core-database-initialisation-strategies
                await _context.Database.EnsureDeletedAsync();
                await _context.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initialising the database.");
                throw;
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        public async Task TrySeedAsync()
        {
            await SeedRolesAsync();

            await SeedUsersAsync();

            await SeedCoreDataAsync();

        }

        private async Task SeedRolesAsync()
        {
            var rolesToSeed = Enum.GetValues<UserRole>()
                .Select(role => role.ToString())
                .ToList();

            if (!rolesToSeed.Contains("Administrator"))
            {
                rolesToSeed.Add("Administrator");
            }

            foreach (var roleName in rolesToSeed)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var role = new ApplicationRole(roleName);
                    var result = await _roleManager.CreateAsync(role);

                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Created role: {RoleName}", roleName);
                    }
                    else
                    {
                        _logger.LogError("Failed to create role: {RoleName}. Errors: {Errors}",
                            roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                        throw new Exception($"Failed to create role: {roleName}");
                    }
                }
                else
                {
                    _logger.LogInformation("Role already exists: {RoleName}", roleName);
                }
            }
        }

        private async Task SeedUsersAsync()
        {
            var usersToSeed = new List<object>
            {
                new { UserType = "Admin", UserName = "admin@ricepro.com", Email = "admin@ricepro.com", Password = "Admin123!", FullName = "System Administrator", PhoneNumber = "+1234567890" },
                new { UserType = "Admin", UserName = "admin2@ricepro.com", Email = "admin2@ricepro.com", Password = "Admin123!", FullName = "Secondary Admin", PhoneNumber = "+1234567891" },

                new { UserType = "AgronomyExpert", UserName = "expert1@ricepro.com", Email = "expert1@ricepro.com", Password = "Expert123!", FullName = "Dr. John Smith", PhoneNumber = "+1234567892", Specialization = "Rice Varieties", ExperienceYears = 15 },
                new { UserType = "AgronomyExpert", UserName = "expert2@ricepro.com", Email = "expert2@ricepro.com", Password = "Expert123!", FullName = "Dr. Sarah Johnson", PhoneNumber = "+1234567893", Specialization = "Pest Management", ExperienceYears = 12 },

                new { UserType = "ClusterManager", UserName = "cluster1@ricepro.com", Email = "cluster1@ricepro.com", Password = "Manager123!", FullName = "Mike Wilson", PhoneNumber = "+1234567894", EmployeeId = "CM001" },
                new { UserType = "ClusterManager", UserName = "cluster2@ricepro.com", Email = "cluster2@ricepro.com", Password = "Manager123!", FullName = "Lisa Chen", PhoneNumber = "+1234567895", EmployeeId = "CM002" },

                new { UserType = "Supervisor", UserName = "supervisor1@ricepro.com", Email = "supervisor1@ricepro.com", Password = "Super123!", FullName = "Robert Brown", PhoneNumber = "+1234567896", EmployeeId = "SUP001" },
                new { UserType = "Supervisor", UserName = "supervisor2@ricepro.com", Email = "supervisor2@ricepro.com", Password = "Super123!", FullName = "Maria Garcia", PhoneNumber = "+1234567897", EmployeeId = "SUP002" },
                new { UserType = "Supervisor", UserName = "supervisor3@ricepro.com", Email = "supervisor3@ricepro.com", Password = "Super123!", FullName = "David Lee", PhoneNumber = "+1234567898", EmployeeId = "SUP003" },

                new { UserType = "Farmer", UserName = "farmer1@ricepro.com", Email = "farmer1@ricepro.com", Password = "Farmer123!", FullName = "Tom Anderson", PhoneNumber = "+1234567899", FarmSize = 5.5m, FarmLocation = "Delta Region A" },
                new { UserType = "Farmer", UserName = "farmer2@ricepro.com", Email = "farmer2@ricepro.com", Password = "Farmer123!", FullName = "Anna Martinez", PhoneNumber = "+1234567800", FarmSize = 8.2m, FarmLocation = "Delta Region B" },
                new { UserType = "Farmer", UserName = "farmer3@ricepro.com", Email = "farmer3@ricepro.com", Password = "Farmer123!", FullName = "Kevin Park", PhoneNumber = "+1234567801", FarmSize = 12.0m, FarmLocation = "Highland Region" },
                new { UserType = "Farmer", UserName = "farmer4@ricepro.com", Email = "farmer4@ricepro.com", Password = "Farmer123!", FullName = "Emily Wong", PhoneNumber = "+1234567802", FarmSize = 6.8m, FarmLocation = "Coastal Region" },

                new { UserType = "UavVendor", UserName = "uav1@ricepro.com", Email = "uav1@ricepro.com", Password = "Vendor123!", FullName = null as string, CompanyName = "SkyTech Drones", ContactPerson = "Alex Thompson", PhoneNumber = "+1234567803", ServiceRadius = 50.0m },
                new { UserType = "UavVendor", UserName = "uav2@ricepro.com", Email = "uav2@ricepro.com", Password = "Vendor123!", FullName = null as string, CompanyName = "AgriAir Solutions", ContactPerson = "Jessica Liu", PhoneNumber = "+1234567804", ServiceRadius = 75.0m }
            };

            foreach (var userData in usersToSeed)
            {
                var userType = userData.GetType().GetProperty("UserType")?.GetValue(userData)?.ToString();
                var userName = userData.GetType().GetProperty("UserName")?.GetValue(userData)?.ToString();
                var email = userData.GetType().GetProperty("Email")?.GetValue(userData)?.ToString();
                var password = userData.GetType().GetProperty("Password")?.GetValue(userData)?.ToString();
                var fullName = userData.GetType().GetProperty("FullName")?.GetValue(userData)?.ToString();
                var phoneNumber = userData.GetType().GetProperty("PhoneNumber")?.GetValue(userData)?.ToString();

                if (_userManager.Users.Any(u => u.UserName == userName || u.Email == email))
                {
                    _logger.LogInformation("User already exists: {UserName} ({Email})", userName, email);
                    continue;
                }

                ApplicationUser user = userType switch
                {
                    "Admin" => new Admin
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    },
                    "AgronomyExpert" => new AgronomyExpert
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    },
                    "ClusterManager" => new ClusterManager
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    },
                    "Supervisor" => new Supervisor
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    },
                    "Farmer" => new Farmer
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    },
                    "UavVendor" => new UavVendor
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        ServiceRadius = Convert.ToDecimal(userData.GetType().GetProperty("ServiceRadius")?.GetValue(userData)),
                        EmailConfirmed = true
                    },
                    _ => new ApplicationUser
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        EmailConfirmed = true
                    }
                };

                // Create user
                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to seed {UserType}: {UserName}. Errors: {Errors}",
                        userType, userName, string.Join(", ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                UserRole userRoleEnum = userType switch
                {
                    "Admin" => UserRole.Admin,
                    "AgronomyExpert" => UserRole.AgronomyExpert,
                    "ClusterManager" => UserRole.ClusterManager,
                    "Supervisor" => UserRole.Supervisor,
                    "Farmer" => UserRole.Farmer,
                    "UavVendor" => UserRole.UavVendor,
                    _ => UserRole.Farmer 
                };

                string roleName = userRoleEnum.ToString();
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Seeded {UserType}: {UserName} with role: {RoleName}", userType, userName, roleName);
                }
                else
                {
                    _logger.LogError("Created user {UserName} but failed to assign role {RoleName}. Errors: {Errors}",
                        userName, roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    continue;
                }

                if (userRoleEnum == UserRole.Admin)
                {
                    var adminRoleResult = await _userManager.AddToRoleAsync(user, "Administrator");
                    if (adminRoleResult.Succeeded)
                    {
                        _logger.LogInformation("Assigned legacy Administrator role to {UserName}", userName);
                    }
                    else
                    {
                        _logger.LogError("Failed to assign legacy Administrator role to {UserName}. Errors: {Errors}",
                            userName, string.Join(", ", adminRoleResult.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private async Task SeedCoreDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
    }
}