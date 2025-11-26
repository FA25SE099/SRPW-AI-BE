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
            await SeedStandardPlanDataAsync();
            await SeedDemoClusterAndFarmersAsync();
            await SeedEmergencyReportsAsync();

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
            await SeedEmergencyReportsAsync();
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

                ("Farmer", "farmer1@ricepro.com", "farmer1@ricepro.com", "Farmer123!", "Tom Anderson", "+1234567899", null, null, null, 5.5m, "Tay Ninh A", null, null, null),
                ("Farmer", "farmer2@ricepro.com", "farmer2@ricepro.com", "Farmer123!", "Anna Martinez", "+1234567800", null, null, null, 8.2m, "Tay Ninh B", null, null, null),
                ("Farmer", "farmer3@ricepro.com", "farmer3@ricepro.com", "Farmer123!", "Kevin Park", "+1234567801", null, null, null, 12.0m, "Tay Ninh C", null, null, null),
                ("Farmer", "farmer4@ricepro.com", "farmer4@ricepro.com", "Farmer123!", "Emily Wong", "+1234567802", null, null, null, 6.8m, "Tay Ninh D", null, null, null),
                ("Farmer", "farmer5@ricepro.com", "farmer5@ricepro.com", "Farmer123!", "John Wick", "+1234567809", null, null, null, 6.8m, "Tay Ninh E", null, null, null),

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
                ("AgronomyExpert", "expert1@ricepro.com", "expert1@ricepro.com", "Expert123!", "Dr. John Smith", "+1234567892", "Rice Varieties", 15, null, null, null, null, null, null),
                ("ClusterManager", "clustermgr@ricepro.com", "clustermgr@ricepro.com", "Manager123!", "Mike Wilson", "+1234567894", null, null, "CM001", null, null, null, null, null),
                ("Farmer", "demo.farmer1@ricepro.com", "demo.farmer1@ricepro.com", "Farmer123!", "Nguyen Van A", "+1234567810", null, null, null, 4.5m, "Demo Area A", null, null, null),
                ("Farmer", "demo.farmer2@ricepro.com", "demo.farmer2@ricepro.com", "Farmer123!", "Tran Van B", "+1234567811", null, null, null, 5.2m, "Demo Area B", null, null, null),
                ("Farmer", "demo.farmer3@ricepro.com", "demo.farmer3@ricepro.com", "Farmer123!", "Le Thi C", "+1234567812", null, null, null, 6.0m, "Demo Area C", null, null, null),
                ("Farmer", "demo.farmer4@ricepro.com", "demo.farmer4@ricepro.com", "Farmer123!", "Pham Van D", "+1234567813", null, null, null, 3.8m, "Demo Area D", null, null, null),
                ("Farmer", "demo.farmer5@ricepro.com", "demo.farmer5@ricepro.com", "Farmer123!", "Hoang Thi E", "+1234567814", null, null, null, 5.5m, "Demo Area E", null, null, null),

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

        #region Emergency Reports Seeding
        private async Task SeedEmergencyReportsAsync()
        {
            if (_context.Set<EmergencyReport>().Any())
            {
                _logger.LogInformation("Emergency reports already exist - skipping");
                return;
            }

            var farmer1 = await _userManager.FindByEmailAsync("demo.farmer1@ricepro.com") as Farmer;
            if (farmer1 == null)
            {
                _logger.LogError("Farmer not found for seeding reports");
                return;
            }

            var plotCultivation = await _context.Set<PlotCultivation>()
                .Include(pc => pc.Plot)
                .FirstOrDefaultAsync(pc => pc.Plot.FarmerId == farmer1.Id);

            if (plotCultivation == null)
            {
                _logger.LogError("No plot cultivation found for farmer");
                return;
            }

            var emergencyReport = new EmergencyReport
            {
                Id = Guid.NewGuid(),
                Source = AlertSource.FarmerReport,
                Severity = AlertSeverity.High,
                Status = AlertStatus.Pending,
                PlotCultivationId = plotCultivation.Id,
                AlertType = "Pest",
                Title = "Brown planthopper infestation detected",
                Description = "Noticed severe brown planthopper infestation in the southern section of the plot. Plants are showing yellowing and wilting symptoms. Immediate action required.",
                ImageUrls = new List<string> { "https://example.com/planthopper1.jpg", "https://example.com/planthopper2.jpg" },
                Coordinates = "10.881,106.711",
                ReportedBy = farmer1.Id,
                NotificationSentAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddHours(-3),
                LastModified = DateTime.UtcNow.AddHours(-3)
            };

            await _context.Set<EmergencyReport>().AddAsync(emergencyReport);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded 1 emergency report");
        }
        #endregion

        #region Demo Cluster and Farmers Seeding
        private async Task SeedDemoClusterAndFarmersAsync()
        {
            if (_context.Set<Cluster>().Any(c => c.ClusterName == "Demo Cluster"))
            {
                _logger.LogInformation("Demo cluster already exists - skipping");
                return;
            }

            var clusterManager = await _userManager.FindByEmailAsync("clustermgr@ricepro.com") as ClusterManager;
            if (clusterManager == null)
            {
                _logger.LogError("Cluster manager not found");
                return;
            }

            var thuDong = await _context.Seasons.FirstOrDefaultAsync(s => s.SeasonName == "Thu Đông");
            var st25 = await _context.RiceVarieties.FirstOrDefaultAsync(v => v.VarietyName == "ST25");

            if (thuDong == null || st25 == null)
            {
                _logger.LogError("Required season or rice variety not found");
                return;
            }

            // Create Demo Cluster
            var demoClusterId = Guid.NewGuid();
            var polygonDemoCluster = CreatePolygonFromWkt("POLYGON((106.710 10.880, 106.710 10.890, 106.720 10.890, 106.720 10.880, 106.710 10.880))");

            var demoCluster = new Cluster
            {
                Id = demoClusterId,
                ClusterName = "Demo Cluster",
                ClusterManagerId = clusterManager.Id,
                Area = 250.0m,
                Boundary = polygonDemoCluster,
                LastModified = DateTime.UtcNow
            };

            await _context.Clusters.AddAsync(demoCluster);
            await _context.SaveChangesAsync();

            // Update cluster manager
            clusterManager.ClusterId = demoClusterId;
            _context.Update(clusterManager);
            await _context.SaveChangesAsync();

            // Get farmers
            var farmer1 = await _userManager.FindByEmailAsync("demo.farmer1@ricepro.com") as Farmer;
            var farmer2 = await _userManager.FindByEmailAsync("demo.farmer2@ricepro.com") as Farmer;
            var farmer3 = await _userManager.FindByEmailAsync("demo.farmer3@ricepro.com") as Farmer;
            var farmer4 = await _userManager.FindByEmailAsync("demo.farmer4@ricepro.com") as Farmer;
            var farmer5 = await _userManager.FindByEmailAsync("demo.farmer5@ricepro.com") as Farmer;

            if (farmer1 == null || farmer2 == null || farmer3 == null || farmer4 == null || farmer5 == null)
            {
                _logger.LogError("Some farmers not found");
                return;
            }

            // Create plots for farmers
            var plantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc);
            var demoPlots = new List<Plot>
            {
                // Farmer 1 - 2 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer1.Id,
                    SoThua = 100,
                    SoTo = 1,
                    Area = 2.5m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.711, 10.881)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.5m,
                            ExpectedYield = 15.0m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71498059235353 10.884914175930405, 106.71500634870534 10.88494494631992, 106.71505143418733 10.884921977418259, 106.71555851534043 10.88404830600929, 106.71551607799915 10.884048085817966, 106.7148021440646 10.884227751931704, 106.71480011622322 10.884263559457352, 106.71498059235353 10.884914175930405))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer1.Id,
                    SoThua = 101,
                    SoTo = 1,
                    Area = 2.0m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.712, 10.881)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.0m,
                            ExpectedYield = 12.0m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71069457408902 10.884105936898294, 106.71071846252238 10.884055109694685, 106.71207612180888 10.883699319026704, 106.71213186148651 10.883738416923123, 106.71219556397517 10.88406683905012, 106.71216769413752 10.884105936898294, 106.71073438814449 10.884493005318205, 106.71068661127799 10.88444999774083, 106.71069457408902 10.884105936898294))")
                },
                
                // Farmer 2 - 2 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer2.Id,
                    SoThua = 102,
                    SoTo = 2,
                    Area = 2.6m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.713, 10.882)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.6m,
                            ExpectedYield = 15.6m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71070253690021 10.883026834407332, 106.71073438814449 10.882995556015928, 106.71176557217888 10.88272577975404, 106.71181733044978 10.88275314837145, 106.71194871683156 10.883136308751276, 106.71192880980504 10.883175406721506, 106.71073438814449 10.883496009884027, 106.71069059268234 10.883449092369588, 106.71070253690021 10.883026834407332))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer2.Id,
                    SoThua = 103,
                    SoTo = 2,
                    Area = 2.6m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.714, 10.882)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.6m,
                            ExpectedYield = 15.6m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71246520193125 10.884549962144803, 106.7124773433348 10.884470684363805, 106.71283314047417 10.884372696581664, 106.71286735397456 10.884412393761849, 106.71314217194521 10.885425479558137, 106.71311518277992 10.885474251051647, 106.7126604114211 10.885281875234625, 106.71246520193125 10.884549962144803))")
                },
                
                // Farmer 3 - 2 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer3.Id,
                    SoThua = 104,
                    SoTo = 3,
                    Area = 3.0m,
                    SoilType = "Đất nông nghiệp",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.715, 10.883)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 3.0m,
                            ExpectedYield = 18.0m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71475993094629 10.884185857813065, 106.7147898575455 10.88420899070907, 106.71553304049667 10.884007850338875, 106.7155673615681 10.883981671719155, 106.71576933706774 10.883620934353317, 106.7157510766865 10.88360394426607, 106.71475220093299 10.883874493323304, 106.71469404169272 10.883939745415574, 106.71475993094629 10.884185857813065))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer3.Id,
                    SoThua = 105,
                    SoTo = 3,
                    Area = 3.0m,
                    SoilType = "Đất nông nghiệp",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.716, 10.883)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 3.0m,
                            ExpectedYield = 18.0m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71291179093043 10.884394856084313, 106.71294090122916 10.884345214055259, 106.71412842547488 10.88403926673567, 106.71418326435332 10.884056329391782, 106.71425742751671 10.884306040529339, 106.7142414508669 10.884338972609328, 106.7130036923827 10.88465981337437, 106.71296966024477 10.884634018643936, 106.71291179093043 10.884394856084313))")
                },
                
                // Farmer 4 - 2 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer4.Id,
                    SoThua = 106,
                    SoTo = 4,
                    Area = 1.9m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.717, 10.884)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 1.9m,
                            ExpectedYield = 11.4m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71069457408902 10.883589844890736, 106.71073438814449 10.883546837183019, 106.71192482839831 10.883230143871259, 106.71198853088697 10.883249692850825, 106.7120920474311 10.883566386141851, 106.71204427056455 10.883652401545447, 106.71071846252238 10.88400819226824, 106.71069059268234 10.883961274834505, 106.71069457408902 10.883589844890736))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer4.Id,
                    SoThua = 107,
                    SoTo = 4,
                    Area = 1.9m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.718, 10.884)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 1.9m,
                            ExpectedYield = 11.4m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.7145518044752 10.883408825499856, 106.71460057814164 10.883434939117606, 106.71613453708704 10.88302139060329, 106.71632233452652 10.882705425364477, 106.71630426939402 10.882656355947788, 106.7161268171239 10.882584475771338, 106.71607069260227 10.882588139470599, 106.71447202698675 10.883032314320317, 106.71445363139475 10.883083423087626, 106.7145518044752 10.883408825499856))")
                },
                
                // Farmer 5 - 2 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer5.Id,
                    SoThua = 108,
                    SoTo = 5,
                    Area = 2.75m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.711, 10.885)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.75m,
                            ExpectedYield = 16.5m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71465576665639 10.883801812868853, 106.71467954706998 10.883825129275792, 106.71584935841179 10.88349513933052, 106.71590053930089 10.883456879875283, 106.71606663309001 10.883153403591834, 106.71605716856175 10.883100706802935, 106.71601578265847 10.883091484992235, 106.7146030776417 10.883487238839052, 106.71458712618221 10.883520643578692, 106.71458756737309 10.883560472932956, 106.71465576665639 10.883801812868853))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer5.Id,
                    SoThua = 109,
                    SoTo = 5,
                    Area = 2.75m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.712, 10.885)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>
                    {
                        new PlotCultivation
                        {
                            RiceVarietyId = st25.Id,
                            SeasonId = thuDong.Id,
                            PlantingDate = plantingDate,
                            Area = 2.75m,
                            ExpectedYield = 16.5m,
                            Status = CultivationStatus.Planned
                        }
                    },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.7107264253334 10.884543832447122, 106.7107264253334 10.88461420845745, 106.71102901215232 10.884782328858108, 106.71109271464098 10.884782328858108, 106.71226324787057 10.884469546640403, 106.7122950991149 10.884410899937805, 106.71224334084167 10.884180222795635, 106.71217565694866 10.88415285430932, 106.7107264253334 10.884543832447122))")
                }
            };

            await _context.Plots.AddRangeAsync(demoPlots);
            await _context.SaveChangesAsync();

            // Update farmers with cluster
            farmer1.ClusterId = demoClusterId;
            farmer2.ClusterId = demoClusterId;
            farmer3.ClusterId = demoClusterId;
            farmer4.ClusterId = demoClusterId;
            farmer5.ClusterId = demoClusterId;

            _context.Update(farmer1);
            _context.Update(farmer2);
            _context.Update(farmer3);
            _context.Update(farmer4);
            _context.Update(farmer5);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded demo cluster with 5 farmers and 10 plots");
        }
        #endregion


        private async Task SeedEmergencyReportsAsync()
        {
            if (_context.Set<EmergencyReport>().Any())
            {
                _logger.LogInformation("Emergency reports already exist - skipping");
                return;
            }

            // Get required entities
            var expert = await _context.Set<AgronomyExpert>().FirstOrDefaultAsync(e => e.Email == "expert1@ricepro.com");
            var supervisor = await _context.Set<Supervisor>().FirstOrDefaultAsync(s => s.Email == "supervisor1@ricepro.com");
            var farmer1 = await _userManager.FindByEmailAsync("farmer1@ricepro.com");

            // Get the specific plot with SoThua = 1 and SoTo = 2
            var targetPlot = await _context.Plots
                .Include(p => p.PlotCultivations)
                .FirstOrDefaultAsync(p => p.SoThua == 1 && p.SoTo == 2);

            if (targetPlot == null || expert == null || targetPlot.PlotCultivations.Count == 0)
            {
                _logger.LogWarning("Target plot (SoThua=1, SoTo=2) or its cultivation not found for emergency report seeding");
                return;
            }

            var plotCultivationId = targetPlot.PlotCultivations.FirstOrDefault()?.Id;

            var emergencyReports = new List<EmergencyReport>
            {
                // Critical pest infestation - Brown Planthopper
                new EmergencyReport
                {
                    Id = Guid.NewGuid(),
                    Source = AlertSource.AiPest,
                    Severity = AlertSeverity.Critical,
                    Status = AlertStatus.New,
                    PlotCultivationId = plotCultivationId,
                    AlertType = "Pest Infestation",
                    Title = "Rầy nâu mật độ cao - Thửa 1, Tờ 2",
                    Description = "Hệ thống AI phát hiện mật độ rầy nâu vượt ngưỡng nguy hiểm (>1000 con/m²) trên thửa 1, tờ 2. Phát hiện qua ảnh UAV và cảm biến IoT. Cần phun thuốc trừ sâu ngay lập tức trong vòng 24 giờ để tránh thiệt hại nghiêm trọng về năng suất.",
                    RecommendedUrgencyHours = 24,
                    ImageUrls = new List<string>
                    {
                        "https://stickershop.line-scdn.net/stickershop/v1/product/1043153/LINEStorePC/main.png?v=1",
                        "https://stickershop.line-scdn.net/stickershop/v1/product/9044256/LINEStorePC/main.png?v=1",
                        "https://cdn.custom-cursor.com/packs/10851/cute-bugcat-capoo-pack.png"
                    },
                    ReportedBy = supervisor?.Id,
                    NotificationSentAt = DateTime.UtcNow.AddHours(-1),
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    LastModified = DateTime.UtcNow.AddHours(-1)
                },

                //// Urgent - Blast disease early stage
                //new EmergencyReport
                //{
                //    Id = Guid.NewGuid(),
                //    Source = AlertSource.FarmerReport,
                //    Severity = AlertSeverity.Urgent,
                //    Status = AlertStatus.Acknowledged,
                //    PlotCultivationId = plotCultivationId,
                //    AlertType = "Disease Outbreak",
                //    Title = "Bệnh đạo ôn lá giai đoạn đầu - Thửa 1, Tờ 2",
                //    Description = "Nông dân Tom Anderson báo cáo phát hiện các đốm đạo ôn hình thoi màu nâu xám trên lá lúa ở góc Đông Bắc của thửa đất. Ước tính 15-20% diện tích bị ảnh hưởng. Nếu không xử lý ngay sẽ lan rộng trong 48-72 giờ tới. Khuyến nghị phun thuốc trừ nấm Tricyclazole hoặc Isoprothiolane.",
                //    RecommendedUrgencyHours = 48,
                //    ImageUrls = new List<string>
                //    {
                //        "https://storage.ricepro.com/alerts/plot1-2-blast-disease-1.jpg",
                //        "https://storage.ricepro.com/alerts/plot1-2-blast-disease-2.jpg"
                //    },
                //    ReportedBy = farmer1?.Id,
                //    NotificationSentAt = DateTime.UtcNow.AddDays(-1),
                //    NotificationAcknowledgeAt = DateTime.UtcNow.AddHours(-18),
                //    CreatedAt = DateTime.UtcNow.AddDays(-1),
                //    LastModified = DateTime.UtcNow.AddHours(-18)
                //},

                //// Warning - Nitrogen deficiency
                //new EmergencyReport
                //{
                //    Id = Guid.NewGuid(),
                //    Source = AlertSource.SupervisorInspection,
                //    Severity = AlertSeverity.Warning,
                //    Status = AlertStatus.New,
                //    PlotCultivationId = plotCultivationId,
                //    AlertType = "Nutrient Deficiency",
                //    Title = "Thiếu hụt đạm nghiêm trọng - Thửa 1, Tờ 2",
                //    Description = "Kiểm tra thực địa phát hiện hiện tượng vàng lá từ gốc lên ngọn, đặc biệt ở các lá già. Phân tích đất cho thấy hàm lượng N thấp hơn ngưỡng khuyến nghị 30%. Cần bón phân đạm bổ sung (Ure hoặc Ammonium Sulfate) với liều lượng 40-50kg/ha trong vòng 5-7 ngày.",
                //    RecommendedUrgencyHours = 120,
                //    ImageUrls = new List<string>
                //    {
                //        "https://storage.ricepro.com/alerts/plot1-2-nitrogen-deficiency.jpg",
                //        "https://storage.ricepro.com/alerts/plot1-2-soil-test-result.jpg"
                //    },
                //    ReportedBy = supervisor?.Id,
                //    NotificationSentAt = DateTime.UtcNow.AddHours(-4),
                //    CreatedAt = DateTime.UtcNow.AddHours(-6),
                //    LastModified = DateTime.UtcNow.AddHours(-4)
                //},

                //// Resolved - Water stress issue
                //new EmergencyReport
                //{
                //    Id = Guid.NewGuid(),
                //    Source = AlertSource.System,
                //    Severity = AlertSeverity.Critical,
                //    Status = AlertStatus.Resolved,
                //    PlotCultivationId = plotCultivationId,
                //    AlertType = "Water Stress",
                //    Title = "Thiếu nước nghiêm trọng đã xử lý - Thửa 1, Tờ 2",
                //    Description = "Cảm biến độ ẩm đất phát hiện độ ẩm giảm xuống dưới 40% (ngưỡng nguy hiểm) trong 60 giờ liên tục. Hệ thống tự động cảnh báo và khuyến nghị tưới khẩn cấp. Diện tích: 2.00 ha.",
                //    RecommendedUrgencyHours = 12,
                //    ResolvedBy = expert?.Id,
                //    ReportedBy = supervisor?.Id,
                //    ResolvedAt = DateTime.UtcNow.AddDays(-3),
                //    ResolutionNotes = "Đã thực hiện tưới khẩn cấp 800m³ nước vào ngày 22/11. Độ ẩm đất đã phục hồi lên 75% sau 24 giờ. Đã lắp đặt thêm 2 cảm biến độ ẩm để giám sát chặt chẽ hơn. Không phát hiện thiệt hại về cây trồng.",
                //    NotificationSentAt = DateTime.UtcNow.AddDays(-6),
                //    NotificationAcknowledgeAt = DateTime.UtcNow.AddDays(-5),
                //    CreatedAt = DateTime.UtcNow.AddDays(-6),
                //    LastModified = DateTime.UtcNow.AddDays(-3)
                //},

                //// Info - Weed competition warning
                //new EmergencyReport
                //{
                //    Id = Guid.NewGuid(),
                //    Source = AlertSource.SupervisorInspection,
                //    Severity = AlertSeverity.Info,
                //    Status = AlertStatus.Acknowledged,
                //    PlotCultivationId = plotCultivationId,
                //    AlertType = "Weed Infestation",
                //    Title = "Cỏ dại cạnh tranh dinh dưỡng - Thửa 1, Tờ 2",
                //    Description = "Khảo sát phát hiện mật độ cỏ dại tăng cao, chủ yếu là cỏ lồng vực và cỏ đuôi chồn. Ước tính mật độ 30-40 cây/m². Khuyến nghị phun thuốc diệt cỏ Butachlor hoặc Pretilachlor trong vòng 10-14 ngày để tránh cạnh tranh dinh dưỡng và nước với cây lúa.",
                //    RecommendedUrgencyHours = 240,
                //    ImageUrls = new List<string>
                //    {
                //        "https://storage.ricepro.com/alerts/plot1-2-weed-survey-1.jpg",
                //        "https://storage.ricepro.com/alerts/plot1-2-weed-survey-2.jpg"
                //    },
                //    ReportedBy = supervisor?.Id,
                //    NotificationSentAt = DateTime.UtcNow.AddDays(-2),
                //    NotificationAcknowledgeAt = DateTime.UtcNow.AddDays(-2).AddHours(3),
                //    CreatedAt = DateTime.UtcNow.AddDays(-2),
                //    LastModified = DateTime.UtcNow.AddDays(-2).AddHours(3)
                //},

                //// Warning - Weather alert for this specific plot area
                //new EmergencyReport
                //{
                //    Id = Guid.NewGuid(),
                //    Source = AlertSource.AiWeather,
                //    Severity = AlertSeverity.Warning,
                //    Status = AlertStatus.New,
                //    PlotCultivationId = plotCultivationId,
                //    AlertType = "Weather Alert",
                //    Title = "Cảnh báo mưa lớn kéo dài - Thửa 1, Tờ 2",
                //    Description = "Dự báo thời tiết cho biết khu vực sẽ có mưa lớn 100-150mm trong 36-48 giờ tới. Nguy cơ ngập úng cao vì địa hình thấp. Khuyến nghị: (1) Hoãn kế hoạch bón phân, (2) Kiểm tra và chuẩn bị hệ thống thoát nước, (3) Theo dõi mực nước liên tục.",
                //    RecommendedUrgencyHours = 36,
                //    ReportedBy = supervisor?.Id,
                //    NotificationSentAt = DateTime.UtcNow.AddHours(-3),
                //    CreatedAt = DateTime.UtcNow.AddHours(-4),
                //    LastModified = DateTime.UtcNow.AddHours(-3)
                //}
            };

            await _context.Set<EmergencyReport>().AddRangeAsync(emergencyReports);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} emergency reports for plot SoThua=1, SoTo=2", emergencyReports.Count);
        }

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
                //new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc đẻ nhánh", StandardPlanId = planId, ExpectedDurationDays = 20, SequenceOrder = 4, IsMandatory = true, Notes = "Giai đoạn đẻ nhánh, kiểm soát nước và dinh dưỡng." },
                //new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc vươn lóng đến trỗ", StandardPlanId = planId, ExpectedDurationDays = 30, SequenceOrder = 5, IsMandatory = true, Notes = "Từ vươn lóng đến trỗ bông, bón thúc và phòng trừ." },
                //new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Chăm sóc trỗ đến chín", StandardPlanId = planId, ExpectedDurationDays = 25, SequenceOrder = 6, IsMandatory = true, Notes = "Từ trỗ đến chín hạt, tập trung phòng sâu bệnh." },
                //new StandardPlanStage { Id = Guid.NewGuid(), StageName = "Thu hoạch lúa và bảo quản", StandardPlanId = planId, ExpectedDurationDays = 7, SequenceOrder = 7, IsMandatory = true, Notes = "Thu hoạch và bảo quản sau khi chín." }
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
            var farmer5 = await _userManager.FindByEmailAsync("farmer5@ricepro.com") as Farmer;

            //var plots = new List<Plot>
            //{
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer1!.Id, SoThua = 15, SoTo = 36, Area = 5.5m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(105.704, 10.0025)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((105.700 10.000, 105.700 10.005, 105.708 10.005, 105.708 10.000, 105.700 10.000))") },
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer2!.Id, SoThua = 18, SoTo = 12, Area = 12m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(105.8075, 10.105)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((105.800 10.100, 105.800 10.110, 105.815 10.110, 105.815 10.100, 105.800 10.100))") },
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 16, SoTo = 58, Area = 25.0857m, SoilType = "Đất nông nghiệp", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.211290, 106.425131)), Status = PlotStatus.Active },
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 17, SoTo = 58, Area = 25.0857m, SoilType = "Đất nông nghiệp", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.212688, 106.427436)), Status = PlotStatus.Active },
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 20, SoTo = 58, Area = 20.0m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.215, 106.430)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((106.428 11.213, 106.428 11.218, 106.438 11.218, 106.438 11.213, 106.428 11.213))") },
            //    new Plot { Id = Guid.NewGuid(), FarmerId = farmer3!.Id, SoThua = 21, SoTo = 58, Area = 15.0m, SoilType = "Đất phù sa", Coordinate = _geometryFactory.CreatePoint(new Coordinate(11.217, 106.432)), Status = PlotStatus.Active, Boundary = CreatePolygonFromWkt("POLYGON((106.430 11.216, 106.430 11.221, 106.440 11.221, 106.440 11.216, 106.430 11.216))") }
            //};

            var plots = new List<Plot>
            {
                // Farmer 1 (Tom Anderson / Nguyen Van A) - 3 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer1!.Id,
                    SoThua = 1,
                    SoTo = 2,
                    Area = 2.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71512114698693, 10.884419749617606)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71498059235353 10.884914175930405, 106.71500634870534 10.88494494631992, 106.71505143418733 10.884921977418259, 106.71555851534043 10.88404830600929, 106.71551607799915 10.884048085817966, 106.7148021440646 10.884227751931704, 106.71480011622322 10.884263559457352, 106.71498059235353 10.884914175930405))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer1!.Id,
                    SoThua = 2,
                    SoTo = 3,
                    Area = 3.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71142310361594, 10.884090858367305)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = heThu.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 3.00m,
                                ExpectedYield = 12.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71069457408902 10.884105936898294, 106.71071846252238 10.884055109694685, 106.71207612180888 10.883699319026704, 106.71213186148651 10.883738416923123, 106.71219556397517 10.88406683905012, 106.71216769413752 10.884105936898294, 106.71073438814449 10.884493005318205, 106.71068661127799 10.88444999774083, 106.71069457408902 10.884105936898294))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer1!.Id,
                    SoThua = 3,
                    SoTo = 4,
                    Area = 4.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71128862918505, 10.883104485814615)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = heThu.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71070253690021 10.883026834407332, 106.71073438814449 10.882995556015928, 106.71176557217888 10.88272577975404, 106.71181733044978 10.88275314837145, 106.71194871683156 10.883136308751276, 106.71192880980504 10.883175406721506, 106.71073438814449 10.883496009884027, 106.71069059268234 10.883449092369588, 106.71070253690021 10.883026834407332))")
                },

                // Farmer 2 (Anna Martinez / Tran Van B) - 3 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer2!.Id,
                    SoThua = 4,
                    SoTo = 5,
                    Area = 5.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71279322122713, 10.884907475019988)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71246520193125 10.884549962144803, 106.7124773433348 10.884470684363805, 106.71283314047417 10.884372696581664, 106.71286735397456 10.884412393761849, 106.71314217194521 10.885425479558137, 106.71311518277992 10.885474251051647, 106.7126604114211 10.885281875234625, 106.71246520193125 10.884549962144803))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer2!.Id,
                    SoThua = 5,
                    SoTo = 6,
                    Area = 6.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71520161759133, 10.883916295465895)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71475993094629 10.884185857813065, 106.7147898575455 10.88420899070907, 106.71553304049667 10.884007850338875, 106.7155673615681 10.883981671719155, 106.71576933706774 10.883620934353317, 106.7157510766865 10.88360394426607, 106.71475220093299 10.883874493323304, 106.71469404169272 10.883939745415574, 106.71475993094629 10.884185857813065))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer2!.Id,
                    SoThua = 6,
                    SoTo = 7,
                    Area = 7.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71357974501365, 10.88434613552687)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71291179093043 10.884394856084313, 106.71294090122916 10.884345214055259, 106.71412842547488 10.88403926673567, 106.71418326435332 10.884056329391782, 106.71425742751671 10.884306040529339, 106.7142414508669 10.884338972609328, 106.7130036923827 10.88465981337437, 106.71296966024477 10.884634018643936, 106.71291179093043 10.884394856084313))")
                },

                // Farmer 3 (Kevin Park / Le Thi C) - 3 plots
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer3!.Id,
                    SoThua = 8,
                    SoTo = 9,
                    Area = 9.00m,
                    SoilType = "Đất nông nghiệp",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71136654907801, 10.883609895322609)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = heThu.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 9.00m,
                                ExpectedYield = 54.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71069457408902 10.883589844890736, 106.71073438814449 10.883546837183019, 106.71192482839831 10.883230143871259, 106.71198853088697 10.883249692850825, 106.7120920474311 10.883566386141851, 106.71204427056455 10.883652401545447, 106.71071846252238 10.88400819226824, 106.71069059268234 10.883961274834505, 106.71069457408902 10.883589844890736))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer3!.Id,
                    SoThua = 9,
                    SoTo = 10,
                    Area = 10.00m,
                    SoilType = "Đất nông nghiệp",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.7153737069554, 10.883000824161098)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.7145518044752 10.883408825499856, 106.71460057814164 10.883434939117606, 106.71613453708704 10.88302139060329, 106.71632233452652 10.882705425364477, 106.71630426939402 10.882656355947788, 106.7161268171239 10.882584475771338, 106.71607069260227 10.882588139470599, 106.71447202698675 10.883032314320317, 106.71445363139475 10.883083423087626, 106.7145518044752 10.883408825499856))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer3!.Id,
                    SoThua = 10,
                    SoTo = 11,
                    Area = 11.00m,
                    SoilType = "Đất nông nghiệp",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71530575922577, 10.883463309816328)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71465576665639 10.883801812868853, 106.71467954706998 10.883825129275792, 106.71584935841179 10.88349513933052, 106.71590053930089 10.883456879875283, 106.71606663309001 10.883153403591834, 106.71605716856175 10.883100706802935, 106.71601578265847 10.883091484992235, 106.7146030776417 10.883487238839052, 106.71458712618221 10.883520643578692, 106.71458756737309 10.883560472932956, 106.71465576665639 10.883801812868853))")
                },

                // Farmer 4 (Emily Wong / Pham Van D) - 1 plot
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer4!.Id,
                    SoThua = 11,
                    SoTo = 12,
                    Area = 12.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71155635255158, 10.884483418022723)),
                    Status = PlotStatus.Active,

                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = heThu.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 12.00m,
                                ExpectedYield = 72.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.7107264253334 10.884543832447122, 106.7107264253334 10.88461420845745, 106.71102901215232 10.884782328858108, 106.71109271464098 10.884782328858108, 106.71226324787057 10.884469546640403, 106.7122950991149 10.884410899937805, 106.71224334084167 10.884180222795635, 106.71217565694866 10.88415285430932, 106.7107264253334 10.884543832447122))")
                },

                // Farmer 5 (John Wick / Pham Van E) - 2 plot
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer5!.Id,
                    SoThua = 12,
                    SoTo = 13,
                    Area = 13.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71382097089725, 10.885206496978089)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71312240549457 10.88520396005326, 106.713169312366 10.885162750618278, 106.7143599985688 10.884837031366303, 106.71439900917852 10.884860568169174, 106.71444646852979 10.884924503573998, 106.71446776191158 10.885019598721058, 106.71451069172849 10.885197986262185, 106.7144863806538 10.885248097633848, 106.71326547563848 10.885578875554287, 106.71321403325368 10.885527822141142, 106.71312240549457 10.88520396005326))")
                },
                new Plot
                {
                    Id = Guid.NewGuid(),
                    FarmerId = farmer5!.Id,
                    SoThua = 7,
                    SoTo = 8,
                    Area = 8.00m,
                    SoilType = "Đất phù sa",
                    Coordinate = _geometryFactory.CreatePoint(new Coordinate(106.71368350916002, 10.884758009248868)),
                    Status = PlotStatus.Active,
                    PlotCultivations = new List<PlotCultivation>()
                        {
                            new PlotCultivation
                            {
                                RiceVarietyId = st25.Id,
                                SeasonId = thuDong.Id,
                                PlantingDate = new DateTime(2024, 12, 20, 0, 0, 0, DateTimeKind.Utc),
                                Area = 4.00m,
                                ExpectedYield = 24.0m, // 9.00m * 6.00m (area * expected yield per hectare for ST25 in Dong Xuan)
                                Status = CultivationStatus.Planned
                            }
                        },
                    Boundary = CreatePolygonFromWkt("POLYGON((106.71300179153047 10.884759980974295, 106.71304286047365 10.884712701694312, 106.71423574462682 10.884394130944344, 106.71428347722645 10.884407281176607, 106.71437586090417 10.884746521304194, 106.71435189288508 10.884784830072348, 106.71311354389002 10.88513638166961, 106.71308833727983 10.88509315092557, 106.71300179153047 10.884759980974295))")
                }
            };

            await _context.Plots.AddRangeAsync(plots);
            await _context.SaveChangesAsync();
            if (farmer1 != null) { farmer1.ClusterId = cluster1Id; _context.Update(farmer1); }
            if (farmer2 != null) { farmer2.ClusterId = cluster2Id; _context.Update(farmer2); }
            if (farmer3 != null) { farmer3.ClusterId = cluster1Id; _context.Update(farmer3); }
            if (farmer4 != null) { farmer4.ClusterId = cluster2Id; _context.Update(farmer4); }
            if (farmer5 != null) { farmer5.ClusterId = cluster2Id; _context.Update(farmer5); }
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
                    Plots = new List<Plot> { plots[1], plots[2], plots[6], plots[9]  },  // NEW unique plots
                    TotalArea = plots[1].Area + plots[2].Area + plots[6].Area + plots[9].Area,
                    Area = UnionPolygons(new[] { plots[1].Boundary, plots[2].Boundary , plots[6].Boundary , plots[9].Boundary })
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
                    Plots = new List<Plot> { plots[3], plots[5] , plots[10] , plots[11]  },
                    TotalArea = plots[3].Area + plots[5].Area + plots[10].Area + plots[11].Area,
                    Area = UnionPolygons(new[] { plots[3].Boundary, plots[5].Boundary , plots[10].Boundary , plots[11].Boundary })
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
                    Plots = new List<Plot> { plots[0], plots[4] },
                    TotalArea = plots[0].Area + plots[4].Area,
                    Area = UnionPolygons(new[] { plots[0].Boundary, plots[4].Boundary })
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
                    Plots = new List<Plot> { plots[7], plots[8] },
                    TotalArea = plots[7].Area + plots[8].Area,
                    Area = UnionPolygons(new[] { plots[7].Boundary, plots[8].Boundary })
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