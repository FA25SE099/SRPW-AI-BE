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

            await SeedMaterialDataAsync();

            await SeedMaterialPriceDataAsync();

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
        private async Task SeedMaterialDataAsync()
        {
            // Add materials seeding
            if (!_context.Set<Material>().Any())
            {
                var materials = new List<Material>
        {
            // Fertilizer
            new Material
            {
                Id = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                Name = "Phân hữu cơ HTO Green",
                Type = MaterialType.Fertilizer,
                AmmountPerMaterial = 50,
                Unit = "kg",
                Description = "Bón lót trước sạ, Bổ sung vi sinh vật đối kháng Trichoderma",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("98AB7097-ECC9-444B-A9A2-26207E28E679"),
                Name = "Ure",
                Type = MaterialType.Fertilizer,
                AmmountPerMaterial = 50,
                Unit = "kg",
                Description = "Bón sau sạ (7-10 NSS), N:46%",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"),
                Name = "Lúa Xanh Bón Thúc 22-15-5 +1S",
                Type = MaterialType.Fertilizer,
                AmmountPerMaterial = 50,
                Unit = "kg",
                Description = "Bón lần 1 (15 - 18 NSS), Bón Lần 2 (30 - 35 NSS), 22-15-5 +1S",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0"),
                Name = "Lúa Vàng Bón Đòng 15-5-20+ 1S",
                Type = MaterialType.Fertilizer,
                AmmountPerMaterial = 50,
                Unit = "kg",
                Description = "Bón Lần 3 (50 - 55 NSS), 15-5-20+ 1S",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            // Pesticide
            new Material
            {
                Id = new Guid("1385516C-B4A3-4F62-9D4D-D55BFB484C47"),
                Name = "Ốc ôm (Niclosamide: 700g/kg)",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 700,
                Unit = "gr",
                Description = "Phun thuốc trừ ốc Trước Sạ, 70g/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("05949927-5F48-4955-A9A1-6B15E525E8E7"),
                Name = "Sạch Ốc 3.6_400ml ( Abamectin 3.6g/ lít)",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 400,
                Unit = "ml",
                Description = "Phun thuốc trừ ốc Trước Sạ, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5"),
                Name = "Cantanil 500EC ( Thương Mại)",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 1000,
                Unit = "ml",
                Description = "Phun thuốc diệt mầm 0-3NSS, 135ml/ 25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"),
                Name = "Butaco 600EC _450 ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 450,
                Unit = "ml",
                Description = "Phun thuốc diệt mầm 0-3NSS, 135ml/ 25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("4DBE9AC3-4900-4919-B55D-9607F36490D2"),
                Name = "Amino 15SL_500ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 500,
                Unit = "ml",
                Description = "Phun 20-22 NSS, 50ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"),
                Name = "Villa Fuji 100SL 1L",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 1000,
                Unit = "ml",
                Description = "Phun 20-22 NSS, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1"),
                Name = "DT Aba 60.5EC_480ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 480,
                Unit = "ml",
                Description = "Phun 20-22 NSS, 50ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"),
                Name = "DT 11 -  Đâm chồi _ 500ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 500,
                Unit = "ml",
                Description = "Phun 35-38 NSS, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("DB1BB9F3-34FE-419C-860A-99DBEDB69092"),
                Name = "DT Ema 40EC 480ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 480,
                Unit = "ml",
                Description = "Phun 35-38 NSS, 50ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"),
                Name = "Rusem super _7.5g",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 7.5m,
                Unit = "gr",
                Description = "Phun 35-38 NSS, 7.5g/25 lít nước (THEO DỊCH HẠI)",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"),
                Name = "Upper 400SC_ 240ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 240,
                Unit = "ml",
                Description = "Phun 55-60 NSS, 36ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"),
                Name = "Captival 400WP",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 400,
                Unit = "gr",
                Description = "Phun 55-60 NSS, 12.5ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2"),
                Name = "DT 11 - Đòng To_500ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 500,
                Unit = "ml",
                Description = "Phun 55-60 NSS, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"),
                Name = "DT9 Vua vào gạo_ 500ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 500,
                Unit = "ml",
                Description = "Trỗ lẹt xẹt, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"),
                Name = "Amino Gold 15SL_500ml",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 500,
                Unit = "ml",
                Description = "Trỗ lẹt xẹt, 50ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5"),
                Name = "Trắng xanh WP",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 100,
                Unit = "ml",
                Description = "Trỗ lẹt xẹt, 100ml/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            },
            new Material
            {
                Id = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD"),
                Name = "DT 6_ 100g",
                Type = MaterialType.Pesticide,
                AmmountPerMaterial = 100,
                Unit = "gr",
                Description = "Cong trái me, 100g/25 lít nước",
                Manufacturer = "DucThanh",
                IsActive = true
            }};

                await _context.Set<Material>().AddRangeAsync(materials);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} materials", materials.Count);
            }
            else
            {
                _logger.LogInformation("Materials data already exists - skipping seeding");
            }
        }
        private async Task SeedMaterialPriceDataAsync()
        {
            if (!_context.Set<MaterialPrice>().Any())
            {
                var materialPrices = new List<MaterialPrice>
        {
            new MaterialPrice
            {
                MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                PricePerMaterial = 345000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("98AB7097-ECC9-444B-A9A2-26207E28E679"),
                PricePerMaterial = 750000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"),
                PricePerMaterial = 896500,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0"),
                PricePerMaterial = 814500,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("1385516C-B4A3-4F62-9D4D-D55BFB484C47"),
                PricePerMaterial = 36000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("05949927-5F48-4955-A9A1-6B15E525E8E7"),
                PricePerMaterial = 66000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5"),
                PricePerMaterial = 107000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"),
                PricePerMaterial = 100000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("4DBE9AC3-4900-4919-B55D-9607F36490D2"),
                PricePerMaterial = 219000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"),
                PricePerMaterial = 100000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1"),
                PricePerMaterial = 194000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"),
                PricePerMaterial = 86000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("DB1BB9F3-34FE-419C-860A-99DBEDB69092"),
                PricePerMaterial = 314000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"),
                PricePerMaterial = 0,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"),
                PricePerMaterial = 299000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"),
                PricePerMaterial = 25000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2"),
                PricePerMaterial = 90000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"),
                PricePerMaterial = 96000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"),
                PricePerMaterial = 219000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5"),
                PricePerMaterial = 288000,
                ValidFrom = DateTime.UtcNow
            },
            new MaterialPrice
            {
                MaterialId = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD"),
                PricePerMaterial = 26000,
                ValidFrom = DateTime.UtcNow
            }
        };

                await _context.Set<MaterialPrice>().AddRangeAsync(materialPrices);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} material prices", materialPrices.Count);
            }
            else
            {
                _logger.LogInformation("Material prices data already exists - skipping seeding");
            }
        }
        private async Task SeedCoreDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
    }
}