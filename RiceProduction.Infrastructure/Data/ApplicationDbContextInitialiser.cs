using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
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

            await SeedVietnameseRiceDataAsync();
            await SeedMaterialDataAsync();

            await SeedMaterialPriceDataAsync();
            await SeedSeasonalPlanDataAsync();
            await SeedClusterDataAsync();
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
        private async Task SeedVietnameseRiceDataAsync()
        {

            bool dataAlreadySeeded = _context.Seasons.Any(s => s.SeasonName == "Đông Xuân") &&
                                     _context.RiceVarieties.Any(v => v.VarietyName == "ST25");

            if (dataAlreadySeeded)
            {
                _logger.LogInformation("Vietnamese rice data (ĐX, HT, TĐ) has already been seeded.");
                return;
            }

            // ----------------------------------------------------------------------
            // 1. Seed Rice Varieties
            // ----------------------------------------------------------------------

            var riceVarietiesData = new (string Name, int Duration, decimal Yield, string Characteristics)[]
            {
        ("OM5451", 95, 6.50m, "Giống lúa chất lượng cao, hạt dài, cơm dẻo, vị đậm. Phổ biến ở ĐBSCL."),
        ("ST25", 105, 6.00m, "Gạo ngon nhất thế giới, thơm mùi lá dứa, cơm dẻo, vị ngọt hậu. Giống cao cấp."),
        ("ST24", 100, 5.80m, "Giống lúa thơm chất lượng, anh em với ST25. Năng suất và chất lượng tốt."),
        ("Jasmine", 105, 6.00m, "Giống lúa thơm phổ biến cho xuất khẩu, hạt thon dài."),
        ("IR50404", 90, 5.50m, "Giống lúa tẻ thường, năng suất ổn định, chịu phèn mặn tốt. Giá thành thấp."),
        ("Nàng Hoa 9", 110, 6.00m, "Giống lúa thơm, cơm dẻo vừa, để nguội vẫn mềm. Chịu phèn tốt."),
        ("Đài Thơm 8", 100, 7.00m, "Giống lúa chủ lực, năng suất cao, chất lượng tốt, chống chịu sâu bệnh.")
            };

            foreach (var data in riceVarietiesData)
            {
                if (!_context.RiceVarieties.Any(v => v.VarietyName == data.Name))
                {
                    _context.RiceVarieties.Add(new RiceVariety
                    {
                        VarietyName = data.Name,
                        BaseGrowthDurationDays = data.Duration,
                        BaseYieldPerHectare = data.Yield,
                        Characteristics = data.Characteristics,
                        IsActive = true
                    });
                }
            }

            // ----------------------------------------------------------------------
            // 2. Seed Seasons
            // ----------------------------------------------------------------------

            var seasonsData = new (string Name, string Type, string StartDate, string EndDate)[]
            {
        // Tháng/Ngày (MM/dd)
        ("Đông Xuân", "Winter-Spring", "12/01", "04/30"),
        ("Hè Thu", "Summer-Autumn", "05/01", "08/31"),
        ("Thu Đông", "Autumn-Winter", "09/01", "11/30")
            };

            foreach (var data in seasonsData)
            {
                if (!_context.Seasons.Any(s => s.SeasonName == data.Name))
                {
                    _context.Seasons.Add(new Season
                    {
                        SeasonName = data.Name,
                        // Sử dụng thuộc tính string mới (DayMonth)
                        StartDate = data.StartDate,
                        EndDate = data.EndDate,
                        SeasonType = data.Type,
                        IsActive = true
                    });
                }
            }

            await _context.SaveChangesAsync();

            // ----------------------------------------------------------------------
            // 3. Seed RiceVarietySeason Relationships
            // ----------------------------------------------------------------------

            var allVarieties = _context.RiceVarieties.ToList();
            var allSeasons = _context.Seasons.ToList();

            var dongXuanSeason = allSeasons.FirstOrDefault(s => s.SeasonName == "Đông Xuân");
            var heThuSeason = allSeasons.FirstOrDefault(s => s.SeasonName == "Hè Thu");
            var thuDongSeason = allSeasons.FirstOrDefault(s => s.SeasonName == "Thu Đông");

            if (dongXuanSeason == null || heThuSeason == null || thuDongSeason == null)
            {
                _logger.LogError("Required seasons were not found after saving changes.");
                return;
            }

            var varietySeasonData = new List<VarietySeasonSeedData>
    {
        // Đông Xuân
        new VarietySeasonSeedData { VarietyName = "Đài Thơm 8", SeasonId = dongXuanSeason.Id, Duration = 100, Yield = 7.50m, Risk = RiskLevel.Low, Notes = "Năng suất tối ưu.", PlantingStart = "12/05", PlantingEnd = "01/20" },
        new VarietySeasonSeedData { VarietyName = "OM5451", SeasonId = dongXuanSeason.Id, Duration = 90, Yield = 7.00m, Risk = RiskLevel.Low, Notes = "Phù hợp gieo sớm.", PlantingStart = "12/15", PlantingEnd = "01/30" },
        new VarietySeasonSeedData { VarietyName = "ST25", SeasonId = dongXuanSeason.Id, Duration = 100, Yield = 6.50m, Risk = RiskLevel.Low, Notes = "Đảm bảo hương thơm và chất lượng.", PlantingStart = "01/01", PlantingEnd = "02/15" },
        new VarietySeasonSeedData { VarietyName = "Jasmine", SeasonId = dongXuanSeason.Id, Duration = 100, Yield = 6.80m, Risk = RiskLevel.Low, Notes = "Giống xuất khẩu, ít sâu bệnh.", PlantingStart = "12/10", PlantingEnd = "01/25" },

        // Hè Thu
        new VarietySeasonSeedData { VarietyName = "Đài Thơm 8", SeasonId = heThuSeason.Id, Duration = 105, Yield = 6.80m, Risk = RiskLevel.Medium, Notes = "Theo dõi bệnh đạo ôn.", PlantingStart = "05/10", PlantingEnd = "06/15" },
        new VarietySeasonSeedData { VarietyName = "OM5451", SeasonId = heThuSeason.Id, Duration = 95, Yield = 6.20m, Risk = RiskLevel.Medium, Notes = "Ngắn ngày, thu hoạch trước mưa lớn.", PlantingStart = "05/20", PlantingEnd = "06/25" },
        new VarietySeasonSeedData { VarietyName = "ST25", SeasonId = heThuSeason.Id, Duration = 105, Yield = 5.50m, Risk = RiskLevel.Medium, Notes = "Chất lượng dễ bị ảnh hưởng bởi độ ẩm cao.", PlantingStart = "05/01", PlantingEnd = "06/10" },
        new VarietySeasonSeedData { VarietyName = "IR50404", SeasonId = heThuSeason.Id, Duration = 90, Yield = 6.00m, Risk = RiskLevel.Low, Notes = "Giống cứng cây, chịu đựng tốt.", PlantingStart = "06/01", PlantingEnd = "07/15" },

        // Thu Đông
        new VarietySeasonSeedData { VarietyName = "IR50404", SeasonId = thuDongSeason.Id, Duration = 95, Yield = 5.00m, Risk = RiskLevel.Medium, Notes = "Thích hợp cho vùng đất thấp.", PlantingStart = "09/05", PlantingEnd = "10/10" },
        new VarietySeasonSeedData { VarietyName = "Đài Thơm 8", SeasonId = thuDongSeason.Id, Duration = 110, Yield = 6.00m, Risk = RiskLevel.High, Notes = "Chỉ trồng ở khu vực có đê bao kiên cố.", PlantingStart = "09/01", PlantingEnd = "10/05" },
        new VarietySeasonSeedData { VarietyName = "Nàng Hoa 9", SeasonId = thuDongSeason.Id, Duration = 115, Yield = 5.50m, Risk = RiskLevel.High, Notes = "Cần gieo sạ sớm để tránh lũ.", PlantingStart = "08/20", PlantingEnd = "09/30" }
    };

            foreach (var data in varietySeasonData)
            {
                var variety = allVarieties.FirstOrDefault(v => v.VarietyName == data.VarietyName);

                if (variety != null && !_context.RiceVarietySeasons.Any(rvs => rvs.RiceVarietyId == variety.Id && rvs.SeasonId == data.SeasonId))
                {
                    _context.RiceVarietySeasons.Add(new RiceVarietySeason
                    {
                        RiceVarietyId = variety.Id,
                        SeasonId = data.SeasonId,
                        GrowthDurationDays = data.Duration,
                        ExpectedYieldPerHectare = data.Yield,
                        RiskLevel = data.Risk,
                        SeasonalNotes = data.Notes,
                        IsRecommended = true,
                        // Sử dụng thuộc tính string mới (DayMonth)
                        OptimalPlantingStart = data.PlantingStart,
                        OptimalPlantingEnd = data.PlantingEnd
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded Vietnamese rice varieties, seasons (ĐX, HT, TĐ), and relationships with specific attributes.");
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
        private async Task SeedSeasonalPlanDataAsync()
        {
            if (!_context.Set<StandardPlan>().Any(p => p.PlanName.Contains("Vụ")))
            {
                // Query for ST25 RiceVariety
                var st25Variety = await _context.Set<RiceVariety>().FirstOrDefaultAsync(v => v.VarietyName == "ST25");
                if (st25Variety == null)
                {
                    st25Variety = new RiceVariety
                    {
                        Id = new Guid("00000000-0000-0000-0000-000000000001"),
                        VarietyName = "ST25",
                        Description = "Lúa ST25 - Giống lúa chất lượng cao Việt Nam.",
                        IsActive = true
                        // Add other properties as per entity
                    };
                    await _context.Set<RiceVariety>().AddAsync(st25Variety);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Seeded ST25 RiceVariety");
                }
                var riceVarietyId = st25Variety.Id;

                // Query for an AgronomyExpert (assume first active expert or seed one)
                var expert = await _context.Set<AgronomyExpert>().FirstOrDefaultAsync(e => e.IsActive);
                if (expert == null)
                {
                    expert = new AgronomyExpert
                    {
                        Id = new Guid("00000000-0000-0000-0000-000000000002"),
                        FullName = "Expert Đức Thành",
                        Email = "expert@ducthanh.com",
                        IsActive = true
                        // Add other properties as per entity
                    };
                    await _context.Set<AgronomyExpert>().AddAsync(expert);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Seeded default AgronomyExpert");
                }
                var expertId = expert.Id;

                // Query for creator (assume same as expert or a user)
                var creatorId = expertId; // Or query User if separate entity

                var seasonsData = new (string Name, string Type, string StartDate, string EndDate, DateTime SowingDate)[]
                {
            ("Đông Xuân", "Winter-Spring", "12/01", "04/30", new DateTime(2024, 12, 20)),
            ("Hè Thu", "Summer-Autumn", "05/01", "08/31", new DateTime(2025, 5, 15)),
            ("Thu Đông", "Autumn-Winter", "09/01", "11/30", new DateTime(2025, 9, 10))
                };

                var seasonalPlans = new List<StandardPlan>();
                var allStages = new List<StandardPlanStage>();
                var allTasks = new List<StandardPlanTask>();
                var allTaskMaterials = new List<StandardPlanTaskMaterial>();

                foreach (var season in seasonsData)
                {
                    var seasonalPlan = new StandardPlan
                    {
                        Id = Guid.NewGuid(),
                        RiceVarietyId = riceVarietyId,
                        ExpertId = expertId,
                        PlanName = $"Quy Trình Canh Tác Lúa ST25 - Vụ {season.Name} {season.SowingDate.Year}-{season.SowingDate.AddYears(1).Year}",
                        Description = $"Quy trình sản xuất lúa ST25 cho vụ {season.Name} với ngày gieo sạ {season.SowingDate:dd/MM/yyyy}. Mùa: {season.Type}, Thời gian vụ: {season.StartDate} đến {season.EndDate}.",
                        TotalDurationDays = 100, // Approx 90-100 days post-sowing
                        CreatedBy = creatorId,
                        IsActive = true
                    };

                    seasonalPlans.Add(seasonalPlan);

                    // Stages (common structure across seasons)
                    var stages = new List<StandardPlanStage>
            {
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 2,
                    SequenceOrder = 1,
                    IsMandatory = true,
                    Notes = "Chuẩn bị hạt giống và đất trước khi sạ."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 1,
                    SequenceOrder = 2,
                    IsMandatory = true,
                    Notes = "Gieo sạ hạt giống theo hàng."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 15,
                    SequenceOrder = 3,
                    IsMandatory = true,
                    Notes = "Chăm sóc ngay sau sạ, bao gồm trừ sâu bệnh và bón phân đầu."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 20,
                    SequenceOrder = 4,
                    IsMandatory = true,
                    Notes = "Giai đoạn đẻ nhánh, kiểm soát nước và dinh dưỡng."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 30,
                    SequenceOrder = 5,
                    IsMandatory = true,
                    Notes = "Từ vươn lóng đến trỗ bông, bón thúc và phòng trừ."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 25,
                    SequenceOrder = 6,
                    IsMandatory = true,
                    Notes = "Từ trỗ đến chín hạt, tập trung phòng sâu bệnh."
                },
                new StandardPlanStage
                {
                    Id = Guid.NewGuid(),
                    StandardPlanId = seasonalPlan.Id,
                    ExpectedDurationDays = 7,
                    SequenceOrder = 7,
                    IsMandatory = true,
                    Notes = "Thu hoạch và bảo quản sau khi chín."
                }
            };

                    allStages.AddRange(stages);

                    // StandardProductionStageIds (placeholders) - Assume pre-seeded or query similarly
                    var standardProductionStageIds = new Dictionary<string, Guid>
            {
                { "BeforeSowing", new Guid("00000000-0000-0000-0000-000000000004") },
                { "Sowing", new Guid("00000000-0000-0000-0000-000000000005") },
                { "PostSowing", new Guid("00000000-0000-0000-0000-000000000006") },
                { "Tillering", new Guid("00000000-0000-0000-0000-000000000007") },
                { "StemElongation", new Guid("00000000-0000-0000-0000-000000000008") },
                { "HeadingMaturity", new Guid("00000000-0000-0000-0000-000000000009") },
                { "Harvesting", new Guid("00000000-0000-0000-0000-000000000010") }
            };

                    // Tasks based on Excel rows (DaysAfter from "Ngày sau sạ", common across seasons)
                    var tasks = new List<StandardPlanTask>();

                    // Stage 1: Trước sạ (-1)
                    var stage1 = stages.First(s => s.SequenceOrder == 1);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage1.Id,
                        TaskName = "Bón lót",
                        Description = "Bón HTO Green 300 kg/ha trước sạ.",
                        DaysAfter = -1,
                        DurationDays = 1,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });

                    // Stage 2: Sạ hàng (0)
                    var stage2 = stages.First(s => s.SequenceOrder == 2);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage2.Id,
                        TaskName = "Sạ",
                        Description = "Gieo sạ ngày 0.",
                        DaysAfter = 0,
                        DurationDays = 1,
                        TaskType = TaskType.Sowing,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });

                    // Stage 3: Chăm sóc sau sạ (0-3, 5-7, 10)
                    var stage3 = stages.First(s => s.SequenceOrder == 3);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Phòng trừ cỏ - Diệt mầm cỏ",
                        Description = "Phun Butaco 600EC ngày 0-3.",
                        DaysAfter = 0,
                        DurationDays = 3,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Phòng trừ cỏ - Cỏ hậu nảy mầm sớm",
                        Description = "Phun Butaco 600EC + Cantanil ngày 5-7.",
                        DaysAfter = 5,
                        DurationDays = 2,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Bón thúc lần 1",
                        Description = "Bón NPK 22-15-5/22-17-7, 100 kg/ha ngày 10.",
                        DaysAfter = 10,
                        DurationDays = 1,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 3
                    });

                    // Stage 4: Đẻ nhánh (15-18, 20, 25-30)
                    var stage4 = stages.First(s => s.SequenceOrder == 4);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Phòng trừ dịch hại (15-18 ngày)",
                        Description = "Phun DT11 + Amino Gold 15SL + Zilla 100SC ngày 15-18.",
                        DaysAfter = 15,
                        DurationDays = 3,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Bón thúc lần 2",
                        Description = "Bón NPK 22-15-5/22-17-7, 100-150 kg/ha ngày 20.",
                        DaysAfter = 20,
                        DurationDays = 1,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Phòng trừ dịch hại (25-30 ngày)",
                        Description = "Phun DT11 + Hexalazole 300SC + Captival 400WP + muỗi hành ngày 25-30.",
                        DaysAfter = 25,
                        DurationDays = 5,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 3
                    });

                    // Stage 5: Vươn lóng đến trỗ (38-42, 45-50)
                    var stage5 = stages.First(s => s.SequenceOrder == 5);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Bón thúc lần 3",
                        Description = "Bón NPK 20-0-22/25-0-25, 100-150 kg/ha ngày 38-42.",
                        DaysAfter = 38,
                        DurationDays = 4,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Phòng trừ dịch hại (45-50 ngày)",
                        Description = "Phun DT11 (Đồng to) + Hexalazole 300SC + Captival 400WP + Rubbercare 720WP + sâu cuốn lá/rầy cánh trắng ngày 45-50.",
                        DaysAfter = 45,
                        DurationDays = 5,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });

                    // Stage 6: Trỗ đến chín (~60-65, ~70, ~75-80)
                    var stage6 = stages.First(s => s.SequenceOrder == 6);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage6.Id,
                        TaskName = "Phòng trừ dịch hại (Trỗ lẹt xẹt)",
                        Description = "Phun Upper 400SC + DT6 + Amino Gold 15SL + Captival 400WP + trừ sâu đục thân ngày ~60-65.",
                        DaysAfter = 60,
                        DurationDays = 5,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage6.Id,
                        TaskName = "Phòng trừ dịch hại (Trỗ đều)",
                        Description = "Phun Upper 400SC hoặc Ori 150SC + DT9 (Kali sửa Nhật) + vi khuẩn + Captival 400WP ngày ~70.",
                        DaysAfter = 70,
                        DurationDays = 1,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage6.Id,
                        TaskName = "Phòng trừ dịch hại (Cong trái me)",
                        Description = "Phun Upper 400SC + DT9 (Vua vào gạo) + Prochess 250WP ngày ~75-80.",
                        DaysAfter = 75,
                        DurationDays = 5,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 3
                    });

                    // Stage 7: Thu hoạch (~90+)
                    var stage7 = stages.First(s => s.SequenceOrder == 7);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage7.Id,
                        TaskName = "Thu hoạch",
                        Description = "Thu hoạch sau khi chín hoàn toàn.",
                        DaysAfter = 90,
                        DurationDays = 7,
                        TaskType = TaskType.Harvesting,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });

                    allTasks.AddRange(tasks);

                    // TaskMaterials (using existing material GUIDs where matched; new ones would need seeding first)
                    // Note: Some new materials like Zilla 100SC, Hexalazole 300SC, etc., are not in previous seeding; assume added or use placeholders
                    // For brevity, linking to matched ones; extend as needed

                    // Bón lót: HTO Green 300 kg/ha
                    var bonLotTask = tasks.First(t => t.TaskName == "Bón lót");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = bonLotTask.Id,
                        MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"), // HTO Green
                        QuantityPerHa = 300.000m
                    });

                    // Diệt mầm cỏ: Butaco 600EC (assume 1350 ml/ha from previous)
                    var dietMamCoTask = tasks.First(t => t.TaskName == "Phòng trừ cỏ - Diệt mầm cỏ");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = dietMamCoTask.Id,
                        MaterialId = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"), // Butaco
                        QuantityPerHa = 1.350m
                    });

                    // Cỏ hậu nảy mầm: Butaco + Cantanil (1350 ml + 1440 ml)
                    var coHauNayTask = tasks.First(t => t.TaskName == "Phòng trừ cỏ - Cỏ hậu nảy mầm sớm");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = coHauNayTask.Id,
                        MaterialId = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"), // Butaco
                        QuantityPerHa = 1.350m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = coHauNayTask.Id,
                        MaterialId = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5"), // Cantanil
                        QuantityPerHa = 1.440m
                    });

                    // Thúc 1: NPK 22-15-5 100 kg/ha
                    var thuc1Task = tasks.First(t => t.TaskName == "Bón thúc lần 1");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc1Task.Id,
                        MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"), // Lúa Xanh Bón Thúc
                        QuantityPerHa = 100.000m
                    });

                    // Phòng trừ 15-18: DT11 + Amino Gold + Zilla (quantities from previous/approx)
                    var phongTru15Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (15-18 ngày)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru15Task.Id,
                        MaterialId = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"), // DT11 (assume Đâm chồi)
                        QuantityPerHa = 1.000m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru15Task.Id,
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), // Amino Gold
                        QuantityPerHa = 0.500m
                    });
                    // Zilla 100SC: Assume new GUID or skip if not seeded

                    // Thúc 2: NPK 22-15-5 100-150 kg/ha (use 125 avg)
                    var thuc2Task = tasks.First(t => t.TaskName == "Bón thúc lần 2");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc2Task.Id,
                        MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"),
                        QuantityPerHa = 125.000m
                    });

                    // Phòng trừ 25-30: DT11 + Hexalazole + Captival
                    var phongTru25Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (25-30 ngày)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru25Task.Id,
                        MaterialId = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"), // DT11
                        QuantityPerHa = 1.000m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru25Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival (assume Hexalazole similar or separate)
                        QuantityPerHa = 0.125m
                    });
                    // Hexalazole skipped

                    // Thúc 3: NPK 20-0-22 100-150 kg/ha
                    var thuc3Task = tasks.First(t => t.TaskName == "Bón thúc lần 3");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc3Task.Id,
                        MaterialId = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0"), // Lúa Vàng (close match)
                        QuantityPerHa = 125.000m
                    });

                    // Phòng trừ 45-50: DT11 + Hexalazole + Captival + Rubbercare
                    var phongTru45Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (45-50 ngày)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru45Task.Id,
                        MaterialId = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2"), // DT11 Đồng to
                        QuantityPerHa = 1.000m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru45Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 0.125m
                    });
                    // Others skipped

                    // Phòng trừ 60-65: Upper + DT6 + Amino Gold + Captival
                    var phongTru60Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (Trỗ lẹt xẹt)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"), // Upper
                        QuantityPerHa = 0.360m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD"), // DT6
                        QuantityPerHa = 1.000m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), // Amino Gold
                        QuantityPerHa = 0.500m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 0.125m
                    });

                    // Phòng trừ 70: Upper/Ori + DT9 + Captival
                    var phongTru70Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (Trỗ đều)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru70Task.Id,
                        MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"), // Upper
                        QuantityPerHa = 0.360m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru70Task.Id,
                        MaterialId = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"), // DT9
                        QuantityPerHa = 1.000m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru70Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 0.125m
                    });

                    // Phòng trừ 75-80: Upper + DT9 + Prochess
                    var phongTru75Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (Cong trái me)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru75Task.Id,
                        MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"), // Upper
                        QuantityPerHa = 0.360m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru75Task.Id,
                        MaterialId = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"), // DT9 Vua vào gạo
                        QuantityPerHa = 1.000m
                    });
                    // Prochess skipped
                }

                await _context.Set<StandardPlan>().AddRangeAsync(seasonalPlans);
                await _context.SaveChangesAsync();

                await _context.Set<StandardPlanStage>().AddRangeAsync(allStages);
                await _context.SaveChangesAsync();

                await _context.Set<StandardPlanTask>().AddRangeAsync(allTasks);
                await _context.SaveChangesAsync();

                await _context.Set<StandardPlanTaskMaterial>().AddRangeAsync(allTaskMaterials);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seeded {Count} seasonal plans, stages, tasks, and materials", seasonalPlans.Count);
            }
            else
            {
                _logger.LogInformation("Seasonal StandardPlan data already exists - skipping seeding");
            }
        }
        private async Task SeedClusterDataAsync()
    {
            // Kiểm tra xem dữ liệu Cluster đã được thêm chưa
            if (!_context.Set<Cluster>().Any())
            {
                _logger.LogInformation("Seeding Core Data: Clusters and Groups...");

                // ----------------------------------------------------------------------
                // 1. Chuẩn bị các ID cần thiết
                // ----------------------------------------------------------------------

                // Lấy ClusterManager, Supervisor, RiceVariety, Season đã seed trước
                var clusterManager1 = await _context.Set<ClusterManager>()
                    .FirstOrDefaultAsync(cm => cm.Email == "cluster1@ricepro.com");
                var clusterManager2 = await _context.Set<ClusterManager>()
                    .FirstOrDefaultAsync(cm => cm.Email == "cluster2@ricepro.com");

                var supervisor1 = await _context.Set<Supervisor>()
                    .FirstOrDefaultAsync(s => s.Email == "supervisor1@ricepro.com");
                var supervisor2 = await _context.Set<Supervisor>()
                    .FirstOrDefaultAsync(s => s.Email == "supervisor2@ricepro.com");
                var supervisor3 = await _context.Set<Supervisor>()
                    .FirstOrDefaultAsync(s => s.Email == "supervisor3@ricepro.com");

                var riceVarietyST25 = await _context.Set<RiceVariety>()
                    .FirstOrDefaultAsync(v => v.VarietyName == "ST25");
                var riceVarietyDT8 = await _context.Set<RiceVariety>()
                    .FirstOrDefaultAsync(v => v.VarietyName == "Đài Thơm 8");

                var seasonDongXuan = await _context.Set<Season>()
                    .FirstOrDefaultAsync(s => s.SeasonName == "Đông Xuân");
                var seasonHeThu = await _context.Set<Season>()
                    .FirstOrDefaultAsync(s => s.SeasonName == "Hè Thu");
                
                // Kiểm tra điều kiện cần thiết
                if (clusterManager1 == null || supervisor1 == null || riceVarietyST25 == null || seasonDongXuan == null)
                {
                    _logger.LogError("Required users or entities for Core Data seeding not found. Skipping Cluster and Group seeding.");
                    return;
                }

                // Tạo các GUID cho Cluster và Group để dễ dàng tham chiếu
                var cluster1Id = new Guid("4A75A0E6-20A5-4E80-928A-D6A8E19B1A01"); // Đồng Tháp
                var cluster2Id = new Guid("9C0C35B8-8F0E-4D2A-8B6C-C32E8F47C499"); // An Giang
                
                var group1Id = new Guid("67B40A3C-4C9D-4F7F-9A52-E23B9B42B101");
                var group2Id = new Guid("3E8F5D2B-8A1C-4E7A-A1B9-F9C3A021E202");
                var group3Id = new Guid("7F9E1C4D-2B8A-4A9B-B0C1-D8E7F6C5D403");

                // Giả lập Polygon cho Boundary và Area
                // Dùng WKT (Well-Known Text) để tạo Polygon
                var factory = new NetTopologySuite.Geometries.GeometryFactory(new NetTopologySuite.Geometries.PrecisionModel(), 4326);
                
                // Polygon mẫu cho Cluster 1 (Đồng Tháp): Hình chữ nhật đơn giản
                // Lệnh: POLYGON((lon1 lat1, lon2 lat2, lon3 lat3, lon4 lat4, lon1 lat1))
                var wktCluster1 = "POLYGON((105.78 10.45, 105.80 10.45, 105.80 10.47, 105.78 10.47, 105.78 10.45))";
                var polygonCluster1 = new NetTopologySuite.IO.WKTReader(factory).Read(wktCluster1) as NetTopologySuite.Geometries.Polygon;

                // Polygon mẫu cho Group 1 (trong Cluster 1)
                var wktGroup1 = "POLYGON((105.785 10.455, 105.795 10.455, 105.795 10.465, 105.785 10.465, 105.785 10.455))";
                var polygonGroup1 = new NetTopologySuite.IO.WKTReader(factory).Read(wktGroup1) as NetTopologySuite.Geometries.Polygon;

                // Polygon mẫu cho Cluster 2 (An Giang)
                var wktCluster2 = "POLYGON((105.15 10.50, 105.18 10.50, 105.18 10.53, 105.15 10.53, 105.15 10.50))";
                var polygonCluster2 = new NetTopologySuite.IO.WKTReader(factory).Read(wktCluster2) as NetTopologySuite.Geometries.Polygon;

                // Polygon mẫu cho Group 3 (trong Cluster 2)
                var wktGroup3 = "POLYGON((105.16 10.51, 105.17 10.51, 105.17 10.52, 105.16 10.52, 105.16 10.51))";
                var polygonGroup3 = new NetTopologySuite.IO.WKTReader(factory).Read(wktGroup3) as NetTopologySuite.Geometries.Polygon;


                // ----------------------------------------------------------------------
                // 2. Seed Clusters
                // ----------------------------------------------------------------------

                var clusters = new List<Cluster>
        {
            new Cluster
            {
                Id = cluster1Id,
                ClusterName = "Cụm Đồng Tháp A",
                ClusterManagerId = clusterManager1.Id,
                Boundary = polygonCluster1,
                Area = 450.75m,
                LastModified = DateTime.UtcNow
            },
            // ... (Cluster 2 và Cluster 3 giữ nguyên, nhớ đổi Created/LastModified thành DateTime.UtcNow) ...
            new Cluster
            {
                Id = cluster2Id,
                ClusterName = "Cụm An Giang B",
                ClusterManagerId = clusterManager2?.Id,
                Boundary = polygonCluster2,
                Area = 680.50m,
                LastModified = DateTime.UtcNow
            },
            new Cluster
            {
                Id = Guid.NewGuid(),
                ClusterName = "Cụm Kiên Giang C (Draft)",
                ClusterManagerId = clusterManager1.Id,
                Area = 300.00m,
                LastModified = DateTime.UtcNow
            }
        };

        await _context.Set<Cluster>().AddRangeAsync(clusters);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} Clusters", clusters.Count);

        // ----------------------------------------------------------------------
        // 3. Seed Groups
        // ----------------------------------------------------------------------
        
        // CHỈNH SỬA: Tạo DateTime với Kind=Utc
        // Lấy ngày hiện tại và ép Kind sang UTC
        var todayUtc = DateTime.UtcNow.Date;
        var plantingDate1 = todayUtc.AddDays(-30); 
        var plantingDate2 = todayUtc.AddDays(-5);  
        var plantingDate4 = todayUtc.AddDays(10); // Kế hoạch sạ 10 ngày tới

        var groups = new List<Group>
        {
            new Group
            {
                Id = group1Id,
                ClusterId = cluster1Id,
                SupervisorId = supervisor1.Id,
                RiceVarietyId = riceVarietyST25.Id,
                SeasonId = seasonDongXuan.Id,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                PlantingDate = plantingDate1,
                IsException = false,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                ReadyForUavDate = plantingDate1.AddDays(40),
                Area = polygonGroup1,
                TotalArea = 25.50m,
                LastModified = DateTime.UtcNow
            },
            new Group
            {
                Id = group2Id,
                ClusterId = cluster1Id,
                SupervisorId = supervisor2?.Id,
                RiceVarietyId = riceVarietyDT8?.Id,
                SeasonId = seasonDongXuan.Id,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                PlantingDate = plantingDate2,
                IsException = false,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                ReadyForUavDate = plantingDate2.AddDays(15),
                TotalArea = 15.00m,
                LastModified = DateTime.UtcNow
            },
            new Group
            {
                Id = group3Id,
                ClusterId = cluster2Id,
                SupervisorId = supervisor3?.Id,
                RiceVarietyId = riceVarietyDT8?.Id,
                SeasonId = seasonHeThu?.Id,
                PlantingDate = null, // Có thể null
                Status = GroupStatus.Draft,
                IsException = true,
                ExceptionReason = "Thiếu thông tin người quản lý và giống lúa.",
                ReadyForUavDate = null, // Có thể null
                Area = polygonGroup3,
                TotalArea = 10.25m,
                LastModified = DateTime.UtcNow
            },
            new Group
            {
                Id = Guid.NewGuid(),
                ClusterId = cluster2Id,
                SupervisorId = supervisor1.Id,
                RiceVarietyId = riceVarietyST25.Id,
                SeasonId = seasonHeThu?.Id,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                PlantingDate = plantingDate4,
                IsException = false,
                // CHỈNH SỬA: Sử dụng PlantingDate đã chuyển sang Kind=Utc
                ReadyForUavDate = plantingDate4.AddDays(15), // Thêm 15 ngày sau khi sạ
                TotalArea = 50.00m,
                LastModified = DateTime.UtcNow
            }
        };

        await _context.Set<Group>().AddRangeAsync(groups);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} Groups", groups.Count);
    }
    else
    {
        _logger.LogInformation("Cluster data already exists - skipping seeding");
    }
    _logger.LogInformation("Core data seeding completed");
        }

        private async Task SeedCoreDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
        private async Task SeedDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
    }
    class VarietySeasonSeedData
    {
        public string VarietyName { get; set; }
        public Guid SeasonId { get; set; }
        public int Duration { get; set; }
        public decimal Yield { get; set; }
        public RiskLevel Risk { get; set; }
        public string Notes { get; set; }
        public string PlantingStart { get; set; }
        public string PlantingEnd { get; set; }
    }
}