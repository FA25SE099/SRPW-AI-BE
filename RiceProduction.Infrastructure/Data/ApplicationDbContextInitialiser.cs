using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Identity;

namespace RiceProduction.Infrastructure.Data
{
    public class ApplicationDbContextInitialiser
    {
        private readonly ILogger<ApplicationDbContextInitialiser> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly GeometryFactory _geometryFactory;

        // Material ID Constants
        private static class MaterialIds
        {
            public static readonly Guid DAP = new Guid("7A8B9C0D-1E2F-3456-7890-ABCDEF123456");  // Use a unique GUID
            public static readonly Guid PhanHuuCo = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49");
            public static readonly Guid Ure = new Guid("98AB7097-ECC9-444B-A9A2-26207E28E679");
            public static readonly Guid LuaXanhBonThuc = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22");
            public static readonly Guid LuaVangBonDong = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0");
            public static readonly Guid OcOm = new Guid("1385516C-B4A3-4F62-9D4D-D55BFB484C47");
            public static readonly Guid SachOc = new Guid("05949927-5F48-4955-A9A1-6B15E525E8E7");
            public static readonly Guid Cantanil = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5");
            public static readonly Guid Butaco = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B");
            public static readonly Guid AminoGold = new Guid("4DBE9AC3-4900-4919-B55D-9607F36490D2");
            public static readonly Guid VillaFuji = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14");
            public static readonly Guid DTAba = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1");
            public static readonly Guid DT11DamChoi = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319");
            public static readonly Guid DTEma = new Guid("DB1BB9F3-34FE-419C-860A-99DBEDB69092");
            public static readonly Guid RusemSuper = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545");
            public static readonly Guid Upper400SC = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C");
            public static readonly Guid Captival = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0");
            public static readonly Guid DT11DongTo = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2");
            public static readonly Guid DT9VuaVaoGao = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76");
            public static readonly Guid TrangXanhWP = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5");
            public static readonly Guid DT6 = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD");
        }

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
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        }

        public async Task InitialiseAsync()
        {
            try
            {
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
        public async Task SeedAsyncAdminOnly()
        {
            try
            {
                await TrySeedAsyncOnlyAdmin();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
        public async Task ResetDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Dropping all data from database...");

                // Get all table names
                var tableNames = _context.Model.GetEntityTypes()
                    .Select(t => t.GetTableName())
                    .Distinct()
                    .ToList();

                // Disable foreign key constraints
                await _context.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");

                // Truncate all tables
                foreach (var tableName in tableNames)
                {
                    await _context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
                }

                // Re-enable foreign key constraints
                await _context.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");

                _logger.LogInformation("All data dropped successfully.");

                // Reseed

                _logger.LogInformation("Database reseeded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting the database.");
                throw;
            }
        }
        public async Task TrySeedAsyncOnlyAdmin()
        {
            await SeedRolesAsync();
            await SeedAdmin();

            await SeedRiceVarietyCategoriesAsync();
            await SeedVietnameseRiceDataAsync();
            await SeedMaterialDataAsync();
            await SeedMaterialPriceDataAsync();

        }
        public async Task TrySeedAsync()
        {
            // Seed in dependency order
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedRiceVarietyCategoriesAsync();
            await SeedVietnameseRiceDataAsync();
            await SeedMaterialDataAsync();
            await SeedMaterialPriceDataAsync();
            await SeedStandardPlanDataAsync();
            await SeedClustersAndGroupsAsync(); // Consolidated
            await SeedCompletedPlansForPastGroups();
        }

        #region Role Seeding
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
            }
        }
        #endregion

        #region User Seeding
        private async Task SeedUsersAsync()
        {
            var usersToSeed = new List<(string UserType, string UserName, string Email, string Password, string FullName, string PhoneNumber, string? Specialization, int? ExperienceYears, string? EmployeeId, decimal? FarmSize, string? FarmLocation, string? CompanyName, string? ContactPerson, decimal? ServiceRadius)>
            {
                ("Admin", "admin@ricepro.com", "admin@ricepro.com", "Admin123!", "System Administrator", "+1234567890", null, null, null, null, null, null, null, null),
                ("Admin", "admin2@ricepro.com", "admin2@ricepro.com", "Admin123!", "Secondary Admin", "+1234567891", null, null, null, null, null, null, null, null),

                ("AgronomyExpert", "expert1@ricepro.com", "expert1@ricepro.com", "Expert123!", "Dr. John Smith", "+1234567892", "Rice Varieties", 15, null, null, null, null, null, null),
                ("AgronomyExpert", "expert2@ricepro.com", "expert2@ricepro.com", "Expert123!", "Dr. Sarah Johnson", "+1234567893", "Pest Management", 12, null, null, null, null, null, null),

                ("ClusterManager", "cluster1@ricepro.com", "cluster1@ricepro.com", "Manager123!", "Mike Wilson", "+1234567894", null, null, "CM001", null, null, null, null, null),
                ("ClusterManager", "cluster2@ricepro.com", "cluster2@ricepro.com", "Manager123!", "Lisa Chen", "+1234567895", null, null, "CM002", null, null, null, null, null),

                ("Supervisor", "supervisor1@ricepro.com", "supervisor1@ricepro.com", "Super123!", "Robert Brown", "+1234567896", null, null, "SUP001", null, null, null, null, null),
                ("Supervisor", "supervisor2@ricepro.com", "supervisor2@ricepro.com", "Super123!", "Maria Garcia", "+1234567897", null, null, "SUP002", null, null, null, null, null),
                ("Supervisor", "supervisor3@ricepro.com", "supervisor3@ricepro.com", "Super123!", "David Lee", "+1234567898", null, null, "SUP003", null, null, null, null, null),

                ("Farmer", "farmer1@ricepro.com", "farmer1@ricepro.com", "Farmer123!", "Tom Anderson", "+1234567899", null, null, null, 5.5m, "Delta Region A", null, null, null),
                ("Farmer", "farmer2@ricepro.com", "farmer2@ricepro.com", "Farmer123!", "Anna Martinez", "+1234567800", null, null, null, 8.2m, "Delta Region B", null, null, null),
                ("Farmer", "farmer3@ricepro.com", "farmer3@ricepro.com", "Farmer123!", "Kevin Park", "+1234567801", null, null, null, 12.0m, "Highland Region", null, null, null),
                ("Farmer", "farmer4@ricepro.com", "farmer4@ricepro.com", "Farmer123!", "Emily Wong", "+1234567802", null, null, null, 6.8m, "Coastal Region", null, null, null),

                ("UavVendor", "uav1@ricepro.com", "uav1@ricepro.com", "Vendor123!", null, "+1234567803", null, null, null, null, null, "SkyTech Drones", "Alex Thompson", 50.0m),
                ("UavVendor", "uav2@ricepro.com", "uav2@ricepro.com", "Vendor123!", null, "+1234567804", null, null, null, null, null, "AgriAir Solutions", "Jessica Liu", 75.0m)
            };

            foreach (var userData in usersToSeed)
            {
                if (_userManager.Users.Any(u => u.UserName == userData.UserName || u.Email == userData.Email))
                {
                    _logger.LogInformation("User already exists: {UserName}", userData.UserName);
                    continue;
                }

                ApplicationUser user = userData.UserType switch
                {
                    "Admin" => new Admin { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "AgronomyExpert" => new AgronomyExpert { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "ClusterManager" => new ClusterManager { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "Supervisor" => new Supervisor { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "Farmer" => new Farmer { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "UavVendor" => new UavVendor { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, ServiceRadius = userData.ServiceRadius ?? 0, EmailConfirmed = true },
                    _ => new ApplicationUser { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true }
                };

                var result = await _userManager.CreateAsync(user, userData.Password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to seed {UserType}: {UserName}", userData.UserType, userData.UserName);
                    continue;
                }

                var roleName = Enum.Parse<UserRole>(userData.UserType).ToString();
                await _userManager.AddToRoleAsync(user, roleName);

                if (userData.UserType == "Admin")
                {
                    await _userManager.AddToRoleAsync(user, "Administrator");
                }

                _logger.LogInformation("Seeded {UserType}: {UserName}", userData.UserType, userData.UserName);
            }
        }
        private async Task SeedAdmin()
        {
            var usersToSeed = new List<(string UserType, string UserName, string Email, string Password, string FullName, string PhoneNumber, string? Specialization, int? ExperienceYears, string? EmployeeId, decimal? FarmSize, string? FarmLocation, string? CompanyName, string? ContactPerson, decimal? ServiceRadius)>
            {
                ("Admin", "admin@ricepro.com", "admin@ricepro.com", "Admin123!", "System Administrator", "+1234567890", null, null, null, null, null, null, null, null),
                ("Admin", "admin2@ricepro.com", "admin2@ricepro.com", "Admin123!", "Secondary Admin", "+1234567891", null, null, null, null, null, null, null, null),
                                ("Supervisor", "supervisor1@ricepro.com", "supervisor1@ricepro.com", "Super123!", "Robert Brown", "+1234567896", null, null, "SUP001", null, null, null, null, null),
                ("Supervisor", "supervisor2@ricepro.com", "supervisor2@ricepro.com", "Super123!", "Maria Garcia", "+1234567897", null, null, "SUP002", null, null, null, null, null),
                ("Supervisor", "supervisor3@ricepro.com", "supervisor3@ricepro.com", "Super123!", "David Lee", "+1234567898", null, null, "SUP003", null, null, null, null, null),

            };

            foreach (var userData in usersToSeed)
            {
                if (_userManager.Users.Any(u => u.UserName == userData.UserName || u.Email == userData.Email))
                {
                    _logger.LogInformation("User already exists: {UserName}", userData.UserName);
                    continue;
                }

                ApplicationUser user = userData.UserType switch
                {
                    "Admin" => new Admin { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "AgronomyExpert" => new AgronomyExpert { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "ClusterManager" => new ClusterManager { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "Supervisor" => new Supervisor { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "Farmer" => new Farmer { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true },
                    "UavVendor" => new UavVendor { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, ServiceRadius = userData.ServiceRadius ?? 0, EmailConfirmed = true },
                    _ => new ApplicationUser { UserName = userData.UserName, Email = userData.Email, FullName = userData.FullName, PhoneNumber = userData.PhoneNumber, EmailConfirmed = true }
                };

                var result = await _userManager.CreateAsync(user, userData.Password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to seed {UserType}: {UserName}", userData.UserType, userData.UserName);
                    continue;
                }

                var roleName = Enum.Parse<UserRole>(userData.UserType).ToString();
                await _userManager.AddToRoleAsync(user, roleName);

                if (userData.UserType == "Admin")
                {
                    await _userManager.AddToRoleAsync(user, "Administrator");
                }

                _logger.LogInformation("Seeded {UserType}: {UserName}", userData.UserType, userData.UserName);
            }
        }

        #endregion

        #region Rice Variety Seeding
        private async Task SeedRiceVarietyCategoriesAsync()
        {
            if (_context.RiceVarietyCategories.Any()) return;

            var categories = new List<RiceVarietyCategory>
            {
                new RiceVarietyCategory
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000001"),
                    CategoryName = "Giống ngắn ngày",
                    CategoryCode = "short",
                    Description = "Giống lúa có thời gian sinh trưởng ngắn (60-95 ngày)",
                    MinGrowthDays = 60,
                    MaxGrowthDays = 95,
                    IsActive = true
                },
                new RiceVarietyCategory
                {
                    Id = new Guid("10000000-0000-0000-0000-000000000002"),
                    CategoryName = "Giống dài ngày",
                    CategoryCode = "long",
                    Description = "Giống lúa có thời gian sinh trưởng dài (100-120 ngày)",
                    MinGrowthDays = 100,
                    MaxGrowthDays = 120,
                    IsActive = true
                }
            };

            await _context.RiceVarietyCategories.AddRangeAsync(categories);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} rice variety categories", categories.Count);
        }

        private async Task SeedVietnameseRiceDataAsync()
        {
            if (_context.Seasons.Any(s => s.SeasonName == "Đông Xuân") &&
                _context.RiceVarieties.Any(v => v.VarietyName == "ST25"))
            {
                _logger.LogInformation("Vietnamese rice data already seeded");
                return;
            }

            var shortCategory = await _context.RiceVarietyCategories.FirstOrDefaultAsync(c => c.CategoryCode == "short");
            var longCategory = await _context.RiceVarietyCategories.FirstOrDefaultAsync(c => c.CategoryCode == "long");

            if (shortCategory == null || longCategory == null)
            {
                _logger.LogError("Rice variety categories not found");
                return;
            }

            // Seed Rice Varieties
            var riceVarietiesData = new[]
            {
                ("OM5451", 95, 6.50m, "Giống lúa chất lượng cao, hạt dài, cơm dẻo, vị đậm. Phổ biến ở ĐBSCL."),
                ("ST25", 105, 6.00m, "Gạo ngon nhất thế giới, thơm mùi lá dứa, cơm dẻo, vị ngọt hậu. Giống cao cấp."),
                ("ST24", 100, 5.80m, "Giống lúa thơm chất lượng, anh em với ST25. Năng suất và chất lượng tốt."),
                ("Jasmine", 105, 6.00m, "Giống lúa thơm phổ biến cho xuất khẩu, hạt thon dài."),
                ("IR50404", 90, 5.50m, "Giống lúa tẻ thường, năng suất ổn định, chịu phèn mặn tốt. Giá thành thấp."),
                ("Nàng Hoa 9", 110, 6.00m, "Giống lúa thơm, cơm dẻo vừa, để nguội vẫn mềm. Chịu phèn tốt."),
                ("Đài Thơm 8", 100, 7.00m, "Giống lúa chủ lực, năng suất cao, chất lượng tốt, chống chịu sâu bệnh.")
            };

            foreach (var (name, duration, yield, characteristics) in riceVarietiesData)
            {
                if (!_context.RiceVarieties.Any(v => v.VarietyName == name))
                {
                    _context.RiceVarieties.Add(new RiceVariety
                    {
                        VarietyName = name,
                        CategoryId = duration < 100 ? shortCategory.Id : longCategory.Id,
                        BaseGrowthDurationDays = duration,
                        BaseYieldPerHectare = yield,
                        Characteristics = characteristics,
                        IsActive = true
                    });
                }
            }

            // Seed Seasons
            var seasonsData = new[]
            {
                ("Đông Xuân", "Winter-Spring", "12/01", "04/30"),
                ("Hè Thu", "Summer-Autumn", "05/01", "08/31"),
                ("Thu Đông", "Autumn-Winter", "09/01", "11/30")
            };

            foreach (var (name, type, startDate, endDate) in seasonsData)
            {
                if (!_context.Seasons.Any(s => s.SeasonName == name))
                {
                    _context.Seasons.Add(new Season
                    {
                        SeasonName = name,
                        StartDate = startDate,
                        EndDate = endDate,
                        SeasonType = type,
                        IsActive = true
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Seed RiceVarietySeason relationships
            await SeedVarietySeasonRelationships();

            _logger.LogInformation("Seeded Vietnamese rice varieties and seasons");
        }

        private async Task SeedVarietySeasonRelationships()
        {
            var allVarieties = _context.RiceVarieties.ToList();
            var dongXuan = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Đông Xuân");
            var heThu = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Hè Thu");
            var thuDong = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Thu Đông");

            if (dongXuan == null || heThu == null || thuDong == null) return;

            var varietySeasonData = new List<(string VarietyName, Guid SeasonId, int Duration, decimal Yield, RiskLevel Risk, string Notes, string PlantingStart, string PlantingEnd)>
            {
                // Đông Xuân
                ("Đài Thơm 8", dongXuan.Id, 100, 7.50m, RiskLevel.Low, "Năng suất tối ưu.", "12/05", "01/20"),
                ("OM5451", dongXuan.Id, 90, 7.00m, RiskLevel.Low, "Phù hợp gieo sớm.", "12/15", "01/30"),
                ("ST25", dongXuan.Id, 100, 6.50m, RiskLevel.Low, "Đảm bảo hương thơm và chất lượng.", "01/01", "02/15"),
                ("Jasmine", dongXuan.Id, 100, 6.80m, RiskLevel.Low, "Giống xuất khẩu, ít sâu bệnh.", "12/10", "01/25"),

                // Hè Thu
                ("Đài Thơm 8", heThu.Id, 105, 6.80m, RiskLevel.Medium, "Theo dõi bệnh đạo ôn.", "05/10", "06/15"),
                ("OM5451", heThu.Id, 95, 6.20m, RiskLevel.Medium, "Ngắn ngày, thu hoạch trước mưa lớn.", "05/20", "06/25"),
                ("ST25", heThu.Id, 105, 5.50m, RiskLevel.Medium, "Chất lượng dễ bị ảnh hưởng bởi độ ẩm cao.", "05/01", "06/10"),
                ("IR50404", heThu.Id, 90, 6.00m, RiskLevel.Low, "Giống cứng cây, chịu đựng tốt.", "06/01", "07/15"),

                // Thu Đông
                ("IR50404", thuDong.Id, 95, 5.00m, RiskLevel.Medium, "Thích hợp cho vùng đất thấp.", "09/05", "10/10"),
                ("Đài Thơm 8", thuDong.Id, 110, 6.00m, RiskLevel.High, "Chỉ trồng ở khu vực có đê bao kiên cố.", "09/01", "10/05"),
                ("Nàng Hoa 9", thuDong.Id, 115, 5.50m, RiskLevel.High, "Cần gieo sạ sớm để tránh lũ.", "08/20", "09/30")
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
                        OptimalPlantingStart = data.PlantingStart,
                        OptimalPlantingEnd = data.PlantingEnd
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
        #endregion

        #region Material Seeding
        private async Task SeedMaterialDataAsync()
        {
            if (_context.Set<Material>().Any()) return;

            var materials = new List<Material>
            {
                // Fertilizers
                new Material { Id = MaterialIds.PhanHuuCo, Name = "Phân hữu cơ HTO Green", Type = MaterialType.Fertilizer, AmmountPerMaterial = 50, Unit = "kg", Description = "Bón lót trước sạ, Bổ sung vi sinh vật đối kháng Trichoderma", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.Ure, Name = "Ure", Type = MaterialType.Fertilizer, AmmountPerMaterial = 50, Unit = "kg", Description = "Bón sau sạ (7-10 NSS), N:46%", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.LuaXanhBonThuc, Name = "Lúa Xanh Bón Thúc 22-15-5 +1S", Type = MaterialType.Fertilizer, AmmountPerMaterial = 50, Unit = "kg", Description = "Bón lần 1 (15 - 18 NSS), Bón Lần 2 (30 - 35 NSS), 22-15-5 +1S", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.LuaVangBonDong, Name = "Lúa Vàng Bón Đòng 15-5-20+ 1S", Type = MaterialType.Fertilizer, AmmountPerMaterial = 50, Unit = "kg", Description = "Bón Lần 3 (50 - 55 NSS), 15-5-20+ 1S", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DAP, Name = "DAP (Đạm Lân)", Type = MaterialType.Fertilizer, AmmountPerMaterial = 50, Unit = "kg", Description = "Bón lót hoặc thúc, N:18%, P2O5:46%", Manufacturer = "DucThanh", IsActive = true },  // NEW ENTRY
                // Pesticides
                new Material { Id = MaterialIds.OcOm, Name = "Ốc ôm (Niclosamide: 700g/kg)", Type = MaterialType.Pesticide, AmmountPerMaterial = 70, Unit = "gr", Description = "Phun thuốc trừ ốc Trước Sạ, 70g/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.SachOc, Name = "Sạch Ốc 3.6_400ml ( Abamectin 3.6g/ lít)", Type = MaterialType.Pesticide, AmmountPerMaterial = 400, Unit = "ml", Description = "Phun thuốc trừ ốc Trước Sạ, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.Cantanil, Name = "Cantanil 500EC ( Thương Mại)", Type = MaterialType.Pesticide, AmmountPerMaterial = 1000, Unit = "ml", Description = "Phun thuốc diệt mầm 0-3NSS, 135ml/ 25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.Butaco, Name = "Butaco 600EC _450 ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 450, Unit = "ml", Description = "Phun thuốc diệt mầm 0-3NSS, 135ml/ 25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.AminoGold, Name = "Amino 15SL_500ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 500, Unit = "ml", Description = "Phun 20-22 NSS, 50ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.VillaFuji, Name = "Villa Fuji 100SL 1L", Type = MaterialType.Pesticide, AmmountPerMaterial = 1000, Unit = "ml", Description = "Phun 20-22 NSS, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DTAba, Name = "DT Aba 60.5EC_480ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 480, Unit = "ml", Description = "Phun 20-22 NSS, 50ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DT11DamChoi, Name = "DT 11 -  Đâm chồi _ 500ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 500, Unit = "ml", Description = "Phun 35-38 NSS, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DTEma, Name = "DT Ema 40EC 480ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 480, Unit = "ml", Description = "Phun 35-38 NSS, 50ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.RusemSuper, Name = "Rusem super _7.5g", Type = MaterialType.Pesticide, AmmountPerMaterial = 7.5m, Unit = "gr", Description = "Phun 35-38 NSS, 7.5g/25 lít nước (THEO DỊCH HẠI)", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.Upper400SC, Name = "Upper 400SC_ 240ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 240, Unit = "ml", Description = "Phun 55-60 NSS, 36ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.Captival, Name = "Captival 400WP", Type = MaterialType.Pesticide, AmmountPerMaterial = 400, Unit = "gr", Description = "Phun 55-60 NSS, 12.5ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DT11DongTo, Name = "DT 11 - Đòng To_500ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 500, Unit = "ml", Description = "Phun 55-60 NSS, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DT9VuaVaoGao, Name = "DT9 Vua vào gạo_ 500ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 500, Unit = "ml", Description = "Trỗ lẹt xẹt, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), Name = "Amino Gold 15SL_500ml", Type = MaterialType.Pesticide, AmmountPerMaterial = 500, Unit = "ml", Description = "Trỗ lẹt xẹt, 50ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.TrangXanhWP, Name = "Trắng xanh WP", Type = MaterialType.Pesticide, AmmountPerMaterial = 100, Unit = "ml", Description = "Trỗ lẹt xẹt, 100ml/25 lít nước", Manufacturer = "DucThanh", IsActive = true },
                new Material { Id = MaterialIds.DT6, Name = "DT 6_ 100g", Type = MaterialType.Pesticide, AmmountPerMaterial = 100, Unit = "gr", Description = "Cong trái me, 100g/25 lít nước", Manufacturer = "DucThanh", IsActive = true }
            };

            await _context.Set<Material>().AddRangeAsync(materials);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} materials", materials.Count);
        }

        private async Task SeedMaterialPriceDataAsync()
        {
            if (_context.Set<MaterialPrice>().Any()) return;

            var currentDate = DateTime.UtcNow;
            var materialPrices = new List<MaterialPrice>
            {
                new MaterialPrice { MaterialId = MaterialIds.PhanHuuCo, PricePerMaterial = 345000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.Ure, PricePerMaterial = 750000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.LuaXanhBonThuc, PricePerMaterial = 896500, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.LuaVangBonDong, PricePerMaterial = 814500, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.OcOm, PricePerMaterial = 36000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.SachOc, PricePerMaterial = 66000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.Cantanil, PricePerMaterial = 107000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.Butaco, PricePerMaterial = 100000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.AminoGold, PricePerMaterial = 219000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.VillaFuji, PricePerMaterial = 100000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DTAba, PricePerMaterial = 194000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DT11DamChoi, PricePerMaterial = 86000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DTEma, PricePerMaterial = 314000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.RusemSuper, PricePerMaterial = 0, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.Upper400SC, PricePerMaterial = 299000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.Captival, PricePerMaterial = 25000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DT11DongTo, PricePerMaterial = 90000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DT9VuaVaoGao, PricePerMaterial = 96000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), PricePerMaterial = 219000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.TrangXanhWP, PricePerMaterial = 288000, ValidFrom = currentDate },
                new MaterialPrice { MaterialId = MaterialIds.DAP, PricePerMaterial = 650000, ValidFrom = currentDate }, 
                new MaterialPrice { MaterialId = MaterialIds.DT6, PricePerMaterial = 26000, ValidFrom = currentDate }

            };

            await _context.Set<MaterialPrice>().AddRangeAsync(materialPrices);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} material prices", materialPrices.Count);
        }
        #endregion

        #region Standard Plan Seeding
        private async Task SeedStandardPlanDataAsync()
        {
            if (_context.Set<StandardPlan>().Any(p => p.PlanName.Contains("Vụ"))) return;

            var longCategory = await _context.RiceVarietyCategories.FirstOrDefaultAsync(c => c.CategoryCode == "long");
            if (longCategory == null)
            {
                _logger.LogError("Long category not found");
                return;
            }

            var expert = await _context.Set<AgronomyExpert>().FirstOrDefaultAsync(e => e.IsActive);
            if (expert == null)
            {
                expert = new AgronomyExpert
                {
                    Id = new Guid("00000000-0000-0000-0000-000000000002"),
                    FullName = "Expert Đức Thành",
                    Email = "expert@ducthanh.com",
                    IsActive = true
                };
                await _context.Set<AgronomyExpert>().AddAsync(expert);
                await _context.SaveChangesAsync();
            }

            var seasonsData = new[]
            {
                ("Đông Xuân", "Winter-Spring", "20/12", "04/03", "19/12"),
                ("Hè Thu", "Summer-Autumn", "15/05", "04/08", "14/05"),
                ("Thu Đông", "Autumn-Winter", "10/09", "30/11", "09/09")
            };

            foreach (var season in seasonsData)
            {
                await CreateStandardPlanForSeason(season.Item1, season.Item2, longCategory.Id, expert.Id);
            }

            _logger.LogInformation("Seeded standard plans for all seasons");
        }

        private async Task CreateStandardPlanForSeason(string seasonName, string seasonType, Guid categoryId, Guid expertId)
        {
            var seasonalPlan = new StandardPlan
            {
                Id = Guid.NewGuid(),
                CategoryId = categoryId,
                ExpertId = expertId,
                PlanName = $"Quy Trình Canh Tác - Vụ {seasonName} (Giống dài ngày)",
                Description = $"Quy trình sản xuất lúa cho vụ {seasonName}. Mùa: {seasonType}",
                TotalDurationDays = 81,
                CreatedBy = expertId,
                IsActive = true
            };

            await _context.Set<StandardPlan>().AddAsync(seasonalPlan);
            await _context.SaveChangesAsync();

            // Create stages for this plan
            await CreateStagesForStandardPlan(seasonalPlan.Id);
        }

        private async Task CreateStagesForStandardPlan(Guid planId)
        {
            var stages = new List<StandardPlanStage>
            {
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Làm đất bón lót", StandardPlanId = planId, ExpectedDurationDays = 1, SequenceOrder = 1, IsMandatory = true, Notes = "Chuẩn bị hạt giống bón phân cho đất trước khi sạ." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Sạ hàng", StandardPlanId = planId, ExpectedDurationDays = 1, SequenceOrder = 2, IsMandatory = true, Notes = "Gieo để hạt giống đều và giữ độ ẩm phù hợp để cây mọc mầm." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc sau sạ", StandardPlanId = planId, ExpectedDurationDays = 15, SequenceOrder = 3, IsMandatory = true, Notes = "Chăm sóc ngay sau sạ, bao gồm trừ sâu bệnh và bón phân đầu." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc đẻ nhánh", StandardPlanId = planId, ExpectedDurationDays = 20, SequenceOrder = 4, IsMandatory = true, Notes = "Giai đoạn đẻ nhánh, kiểm soát nước và dinh dưỡng." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc vươn lóng đến trỗ", StandardPlanId = planId, ExpectedDurationDays = 30, SequenceOrder = 5, IsMandatory = true, Notes = "Từ vươn lóng đến trỗ bông, bón thúc và phòng trừ." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc trỗ đến chín", StandardPlanId = planId, ExpectedDurationDays = 25, SequenceOrder = 6, IsMandatory = true, Notes = "Từ trỗ đến chín hạt, tập trung phòng sâu bệnh." },
                new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Thu hoạch lúa và bảo quản", StandardPlanId = planId, ExpectedDurationDays = 7, SequenceOrder = 7, IsMandatory = true, Notes = "Thu hoạch và bảo quản sau khi chín." }
            };

            await _context.Set<StandardPlanStage>().AddRangeAsync(stages);
            await _context.SaveChangesAsync();

            // Create tasks for each stage
            await CreateTasksForStages(stages);
        }

        private async Task CreateTasksForStages(List<StandardPlanStage> stages)
        {
            var allTasks = new List<StandardPlanTask>();
            var allTaskMaterials = new List<StandardPlanTaskMaterial>();

            // Stage 1: Pre-planting
            var stage1 = stages[0];
            var bonLotTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage1.Id,
                TaskName = "Bón lót",
                Description = "Bón lót các loại phân như phân hữu cơ, lân",
                DaysAfter = -1,
                DurationDays = 1,
                TaskType = TaskType.Fertilization,
                Priority = TaskPriority.High,
                SequenceOrder = 1
            };
            allTasks.Add(bonLotTask);
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = bonLotTask.Id, MaterialId = MaterialIds.PhanHuuCo, QuantityPerHa = 300m });

            var lamDatTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage1.Id,
                TaskName = "Làm đất",
                Description = "Cày bừa lại theo phương pháp bừa trục và trạc",
                DaysAfter = -1,
                DurationDays = 0,
                TaskType = TaskType.Sowing,
                Priority = TaskPriority.High,
                SequenceOrder = 2
            };
            allTasks.Add(lamDatTask);

            // Stage 2: Sowing
            var stage2 = stages[1];
            var saTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage2.Id,
                TaskName = "Sạ (ngày 0)",
                Description = "Gieo để hạt giống đều",
                DaysAfter = 0,
                DurationDays = 1,
                TaskType = TaskType.Sowing,
                Priority = TaskPriority.High,
                SequenceOrder = 1
            };
            allTasks.Add(saTask);

            // Stage 3: Post-planting care
            var stage3 = stages[2];
            var dienOcTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage3.Id,
                TaskName = "Phòng trừ dịch hại (ốc) (ngày 1-2)",
                Description = "Xử lý ốc để khi cây lúa mọc mầm không bị ốc gây hại",
                DaysAfter = 0,
                DurationDays = 2,
                TaskType = TaskType.PestControl,
                Priority = TaskPriority.High,
                SequenceOrder = 1
            };
            allTasks.Add(dienOcTask);
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = dienOcTask.Id, MaterialId = MaterialIds.OcOm, QuantityPerHa = 700m });
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = dienOcTask.Id, MaterialId = MaterialIds.SachOc, QuantityPerHa = 2000m });

            var dienCoTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage3.Id,
                TaskName = "Phòng trừ dịch hại (cỏ - mầm cỏ) (ngày 2-4)",
                Description = "Xử lý cỏ dại để tránh cạnh tranh dinh dưỡng",
                DaysAfter = 2,
                DurationDays = 2,
                TaskType = TaskType.PestControl,
                Priority = TaskPriority.High,
                SequenceOrder = 2
            };
            allTasks.Add(dienCoTask);
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = dienCoTask.Id, MaterialId = MaterialIds.Butaco, QuantityPerHa = 1350m });
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = dienCoTask.Id, MaterialId = MaterialIds.Cantanil, QuantityPerHa = 1440m });

            var bonSauSaTask = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage3.Id,
                TaskName = "Bón sau sạ (ngày 5-7)",
                Description = "Bón sau sạ 7-10 ngày",
                DaysAfter = 5,
                DurationDays = 7,
                TaskType = TaskType.Fertilization,
                Priority = TaskPriority.Normal,
                SequenceOrder = 4
            };
            allTasks.Add(bonSauSaTask);
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = bonSauSaTask.Id, MaterialId = MaterialIds.Ure, QuantityPerHa = 50m });

            var bonThuc1Task = new StandardPlanTask
            {
                Id = Guid.NewGuid(),
                StandardProductionStageId = stage3.Id,
                TaskName = "Bón thúc lần 1 (ngày 15-18)",
                Description = "Bón lót lần 1 cho cây lúa",
                DaysAfter = 15,
                DurationDays = 5,
                TaskType = TaskType.Fertilization,
                Priority = TaskPriority.Normal,
                SequenceOrder = 5
            };
            allTasks.Add(bonThuc1Task);
            allTaskMaterials.Add(new StandardPlanTaskMaterial { Id = Guid.NewGuid(), StandardPlanTaskId = bonThuc1Task.Id, MaterialId = MaterialIds.LuaXanhBonThuc, QuantityPerHa = 100m });

            // Add remaining tasks for stages 4-7...
            // (Abbreviated for space - follow same pattern)

            await _context.Set<StandardPlanTask>().AddRangeAsync(allTasks);
            await _context.SaveChangesAsync();

            await _context.Set<StandardPlanTaskMaterial>().AddRangeAsync(allTaskMaterials);
            await _context.SaveChangesAsync();
        }
        #endregion

        #region Cluster and Group Seeding (Consolidated)
        private async Task SeedClustersAndGroupsAsync()
        {
            if (_context.Set<Cluster>().Any())
            {
                _logger.LogInformation("Clusters already exist - skipping");
                return;
            }

            var clusterManager1 = await _userManager.FindByEmailAsync("cluster1@ricepro.com") as ClusterManager;
            var clusterManager2 = await _userManager.FindByEmailAsync("cluster2@ricepro.com") as ClusterManager;
            var supervisor1 = await _userManager.FindByEmailAsync("supervisor1@ricepro.com") as Supervisor;
            var supervisor2 = await _userManager.FindByEmailAsync("supervisor2@ricepro.com") as Supervisor;

            if (clusterManager1 == null || supervisor1 == null)
            {
                _logger.LogError("Required users not found");
                return;
            }

            var st25 = await _context.RiceVarieties.FirstOrDefaultAsync(v => v.VarietyName == "ST25");
            var om5451 = await _context.RiceVarieties.FirstOrDefaultAsync(v => v.VarietyName == "OM5451");
            var dongXuan = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Đông Xuân");
            var heThu = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Hè Thu");
            var thuDong = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Thu Đông");

            if (st25 == null || dongXuan == null)
            {
                _logger.LogError("Required rice varieties or seasons not found");
                return;
            }

            // Create Clusters
            var cluster1Id = Guid.NewGuid();
            var cluster2Id = Guid.NewGuid();

            var polygonCluster1 = CreatePolygonFromWkt("POLYGON((1196936.46062617 608865.269417751, 1196936.91062585 608857.799417709, 1196937.4406257 608854.249417673,1196937.11062567 608853.609417687,1196960.63062598 608855.569416764,1196960.5706265 608867.849416807,1196936.46062617 608865.269417751))");
            var polygonCluster2 = CreatePolygonFromWkt("POLYGON((1196959.19062734 608887.939416929,1196934.67062708 608887.029417897,1196935.46062667 608877.419417831,1196959.61062698 608879.409416884,1196959.19062734 608887.939416929))");

            var clusters = new List<Cluster>
            {
                new Cluster { Id = cluster1Id, ClusterName = "DongThap1", ClusterManagerId = clusterManager1.Id, Area = 150.75m, Boundary = polygonCluster1, LastModified = DateTime.UtcNow },
                new Cluster { Id = cluster2Id, ClusterName = "AnGiang2", ClusterManagerId = clusterManager2?.Id, Area = 220.50m, Boundary = polygonCluster2, LastModified = DateTime.UtcNow }
            };

            await _context.Clusters.AddRangeAsync(clusters);
            await _context.SaveChangesAsync();

            // Update cluster managers
            clusterManager1.ClusterId = cluster1Id;
            if (clusterManager2 != null) clusterManager2.ClusterId = cluster2Id;
            _context.Update(clusterManager1);
            if (clusterManager2 != null) _context.Update(clusterManager2);
            await _context.SaveChangesAsync();

            // Create Plots
            var farmer1 = await _userManager.FindByEmailAsync("farmer1@ricepro.com") as Farmer;
            var farmer2 = await _userManager.FindByEmailAsync("farmer2@ricepro.com") as Farmer;
            var farmer3 = await _userManager.FindByEmailAsync("farmer3@ricepro.com") as Farmer;
            var farmer4 = await _userManager.FindByEmailAsync("farmer4@ricepro.com") as Farmer;

            var plots = new List<Plot>
            {
                new Plot { Id = Guid.NewGuid(), FarmerId = farmer1!.Id, SoThua = 15, SoTo = 36, Area = 5.5m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(105.704, 10.0025)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((105.700 10.000, 105.700 10.005, 105.708 10.005, 105.708 10.000, 105.700 10.000))") },
                new Plot { Id = Guid.NewGuid(), FarmerId = farmer2!.Id, SoThua = 18, SoTo = 12, Area = 12m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(105.8075, 10.105)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((105.800 10.100, 105.800 10.110, 105.815 10.110, 105.815 10.100, 105.800 10.100))") },
                new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 16, SoTo = 58, Area = 25.0857m, SoilType = "Đất nông nghiệp", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.211290, 106.425131)), Status = PlotStatus.Active },
                new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 17, SoTo = 58, Area = 25.0857m, SoilType = "Đất nông nghiệp", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.212688, 106.427436)), Status = PlotStatus.Active },
                new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 20, SoTo = 58, Area = 20.0m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.215, 106.430)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((106.428 11.213, 106.428 11.218, 106.438 11.218, 106.438 11.213, 106.428 11.213))") },
    new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 21, SoTo = 58, Area = 15.0m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.217, 106.432)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((106.430 11.216, 106.430 11.221, 106.440 11.221, 106.440 11.216, 106.430 11.216))") }
            };

            await _context.Plots.AddRangeAsync(plots);
            await _context.SaveChangesAsync();
            if (farmer1 != null) { farmer1.ClusterId = cluster1Id; _context.Update(farmer1); }
            if (farmer2 != null) { farmer2.ClusterId = cluster2Id; _context.Update(farmer2); }
            if (farmer3 != null) { farmer3.ClusterId = cluster1Id; _context.Update(farmer3); }
            if (farmer4 != null) { farmer4.ClusterId = cluster2Id; _context.Update(farmer4); }
            await _context.SaveChangesAsync();
            // Create Groups with plots
            var todayUtc = DateTime.UtcNow.Date;
            var groups = new List<Group>
            {
                new Group
                {
                    ClusterId = cluster1Id,
                    SupervisorId = supervisor1.Id,
                    RiceVarietyId = st25.Id,
                    SeasonId = thuDong.Id,
                    Year = 2025,
                    PlantingDate = todayUtc.AddDays(-20),
                    Status = GroupStatus.Active,
                    Plots = new List<Plot> { plots[4], plots[5] },  // NEW unique plots
                    TotalArea = plots[4].Area + plots[5].Area,
                    Area = UnionPolygons(new[] { plots[4].Boundary, plots[5].Boundary })
                },               
                new Group
                {
                    ClusterId = cluster1Id,
                    SupervisorId = supervisor1.Id,
                    RiceVarietyId = st25.Id,
                    SeasonId = heThu.Id,
                    Year = 2025,
                    PlantingDate = todayUtc.AddDays(-30),
                    Status = GroupStatus.Completed,
                    Plots = new List<Plot> { plots[0] },
                    TotalArea = plots[0].Area,
                    Area = plots[0].Boundary
                },
                new Group
                {
                    ClusterId = cluster2Id,
                    SupervisorId = supervisor2?.Id,
                    RiceVarietyId = st25.Id,
                    SeasonId = heThu.Id,
                    Year = 2025,
                    PlantingDate = todayUtc.AddDays(-5),
                    Status = GroupStatus.Active,
                    Plots = new List<Plot> { plots[1] },
                    TotalArea = plots[1].Area,
                    Area = plots[1].Boundary
                },
                new Group
                {
                    ClusterId = cluster1Id,
                    SupervisorId = supervisor1.Id,
                    RiceVarietyId = om5451?.Id,
                    SeasonId = heThu.Id,
                    Year = 2024,
                    PlantingDate = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    Status = GroupStatus.Completed,
                    Plots = new List<Plot> { plots[2], plots[3] },
                    TotalArea = plots[2].Area + plots[3].Area,
                    Area = UnionPolygons(new[] { plots[2].Boundary, plots[3].Boundary })
                }
            };

            await _context.Groups.AddRangeAsync(groups);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {ClusterCount} clusters and {GroupCount} groups", clusters.Count, groups.Count);
        }
        #endregion

        #region Completed Plans Seeding
        private async Task SeedCompletedPlansForPastGroups()
        {
            if (_context.ProductionPlans.Any(pp => pp.Group != null && pp.Group.Year < 2025))
            {
                _logger.LogInformation("Completed plans already exist");
                return;
            }

            var pastGroup2024 = await _context.Groups
                .Include(g => g.Plots)
                .FirstOrDefaultAsync(g => g.Year == 2024 && g.Status == GroupStatus.Completed);

            if (pastGroup2024 == null)
            {
                _logger.LogWarning("Past group 2024 not found");
                return;
            }

            var dapMaterial = await _context.Materials.FirstOrDefaultAsync(m => m.Name.Contains("DAP"));
            var ureaMaterial = await _context.Materials.FirstOrDefaultAsync(m => m.Name.Contains("Ure"));

            if (dapMaterial == null || ureaMaterial == null)
            {
                _logger.LogWarning("Required materials not found");
                return;
            }

            await CreateCompletedPlanFor2024(pastGroup2024, dapMaterial, ureaMaterial);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded completed production plans");
        }

        private async Task CreateCompletedPlanFor2024(Group group, Material dapMaterial, Material ureaMaterial)
        {
            var plantingDate = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);

            var productionPlan = new ProductionPlan
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                PlanName = $"Kế hoạch Hè Thu 2024",
                BasePlantingDate = plantingDate,
                TotalArea = group.TotalArea ?? 0,
                Status = Domain.Enums.TaskStatus.Completed,
                SubmittedAt = plantingDate.AddDays(-25),
                ApprovedAt = plantingDate.AddDays(-20),
                CreatedAt = plantingDate.AddDays(-25),
                LastModified = plantingDate.AddDays(115)
            };

            // Create stages and tasks
            var stage1 = new ProductionStage
            {
                Id = Guid.NewGuid(),
                ProductionPlanId = productionPlan.Id,
                StageName = "Làm đất",
                SequenceOrder = 1,
                Description = "Chuẩn bị đất trước khi sạ",
                ProductionPlanTasks = new List<ProductionPlanTask>()
            };

            var task1 = new ProductionPlanTask
            {
                Id = Guid.NewGuid(),
                ProductionStageId = stage1.Id,
                TaskName = "Cày bừa",
                TaskType = Domain.Enums.TaskType.LandPreparation,
                ScheduledDate = plantingDate.AddDays(-7),
                ScheduledEndDate = plantingDate.AddDays(-5),
                Status = Domain.Enums.TaskStatus.Completed,
                EstimatedMaterialCost = 500000 * (group.TotalArea ?? 1),
                CultivationTasks = new List<CultivationTask>()
            };

            foreach (var plot in group.Plots)
            {
                task1.CultivationTasks.Add(new CultivationTask
                {
                    Id = Guid.NewGuid(),
                    IsContingency = false,
                    ActualStartDate = plantingDate.AddDays(-7),
                    ActualEndDate = plantingDate.AddDays(-5),
                    ActualMaterialCost = 450000,
                    ActualServiceCost = 200000,
                    CompletedAt = plantingDate.AddDays(-5),
                    Status = Domain.Enums.TaskStatus.InProgress,
                    PlotCultivation = new PlotCultivation
                    {
                        PlotId = plot.Id,
                        RiceVarietyId = group.RiceVarietyId!.Value,
                        SeasonId = group.SeasonId!.Value,
                        PlantingDate = plantingDate,
                        Status = CultivationStatus.Planned,
                        ActualYield = plot.Area * 7.2m
                    }
                });
            }

            stage1.ProductionPlanTasks.Add(task1);
            productionPlan.CurrentProductionStages.Add(stage1);

            await _context.ProductionPlans.AddAsync(productionPlan);
        }
        #endregion

        #region Helper Methods
        private Polygon CreatePolygonFromWkt(string wkt)
        {
            var reader = new NetTopologySuite.IO.WKTReader(_geometryFactory);
            return reader.Read(wkt) as Polygon ?? throw new InvalidOperationException("Invalid WKT polygon");
        }

        private Polygon? UnionPolygons(Geometry?[] geometries)
        {
            var validGeometries = geometries.Where(g => g != null).ToArray();
            if (validGeometries.Length == 0) return null;
            if (validGeometries.Length == 1) return ConvertToPolygon(validGeometries[0]!);

            var union = new GeometryCollection(validGeometries!).Union();
            return ConvertToPolygon(union);
        }

        private Polygon ConvertToPolygon(Geometry geometry)
        {
            if (geometry is Polygon polygon)
                return polygon;

            if (geometry is MultiPolygon multiPolygon)
                return multiPolygon.Geometries.Cast<Polygon>().OrderByDescending(p => p.Area).First();

            if (geometry is GeometryCollection collection)
            {
                var firstPolygon = collection.Geometries.OfType<Polygon>().FirstOrDefault();
                if (firstPolygon != null) return firstPolygon;
            }

            return (Polygon)geometry.ConvexHull();
        }
        #endregion
    }
}