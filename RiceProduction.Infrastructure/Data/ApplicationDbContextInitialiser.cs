using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using RiceProduction.Domain.Entities;
using RiceProduction.Domain.Enums;
using RiceProduction.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
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
            //await SeedProductionPlanAsync();
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedRiceVarietyCategoriesAsync();
            await SeedVietnameseRiceDataAsync();
            await SeedMaterialDataAsync();
            await SeedMaterialPriceDataAsync();
            await SeedStandardPlanDataAsync();
            await SeedClusterDataAsync();
            await SeedCoreDataAsync();
            await SeedPlotDataAsync();
            await SeedClusterAsync();
            await SeedGroupAsync();
            await SeedProductionTask();
            await SeedDataAsync();
        }

        private async Task SeedProductionTask()
        {

        }
        private async Task SeedGroupAsync()
        {
            if (_context.Groups.Any())
            {
                _logger.LogInformation("Group data already exists. Skipping seeding.");
                return;
            }
            var supervisor1 = await _userManager.FindByEmailAsync("supervisor1@ricepro.com") as Supervisor;
            var supervisor2 = await _userManager.FindByEmailAsync("supervisor2@ricepro.com") as Supervisor;

            var st25Variety = await _context.RiceVarieties.FirstOrDefaultAsync(v => v.VarietyName == "ST25");
            var heThuSeason = await _context.Seasons.FirstOrDefaultAsync(v => v.SeasonName == "Hè Thu");


            var plot1 = await _context.Plots.FirstOrDefaultAsync(p => p.SoThua == 15); //farmer1
            var plot2 = await _context.Plots.FirstOrDefaultAsync(p => p.SoThua == 18);//farmer2
            var plot3 = await _context.Plots.FirstOrDefaultAsync(p => p.SoThua == 17);//farner3
            var plot4 = await _context.Plots.FirstOrDefaultAsync(p => p.SoThua == 16);//farner3



            var cluster1 = await _context.Clusters.FirstOrDefaultAsync(c => c.ClusterName == "DongThap1");
            var cluster2 = await _context.Clusters.FirstOrDefaultAsync(c => c.ClusterName == "AnGiang2");

            if (supervisor1 == null || supervisor2 == null)
            {
                _logger.LogError("Supervisor field is null. Skipping Group seeding");
                return;
            }
            if (cluster1 == null || cluster2 == null)
            {
                _logger.LogError("Cluster field is null. Skipping Group seeding");
                return;
            }
            if (st25Variety == null)
            {
                _logger.LogError("Rice variety is null. Skipping Group seeding");
            }
            if (plot1 == null || plot2 == null || plot3 == null || plot4 == null)
            {
                _logger.LogError("Plot is null. Skipping Group seeding");
            }
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            Geometry CalculateGroupBoundary(List<Plot> plots)
            {
                if (plots.Count == 1)
                    return plots[0].Boundary;

                return new GeometryCollection(plots.Select(p => p.Boundary).ToArray()).Union();
            }

            var plotsForGroup1 = new List<Plot> { plot1 };
            var group1BoundaryGeometry = CalculateGroupBoundary(plotsForGroup1);
            var group1Boundary = ConvertToPolygon(group1BoundaryGeometry);
            var group1TotalArea = plotsForGroup1.Sum(p => p.Area);

            var plotsForGroup2 = new List<Plot> { plot2 };
            var group2BoundaryGeometry = CalculateGroupBoundary(plotsForGroup2);
            var group2Boundary = ConvertToPolygon(group2BoundaryGeometry);
            var group2TotalArea = plotsForGroup2.Sum(p => p.Area);

            var plotsForGroup3 = new List<Plot> { plot3, plot4 };
            var group3BoundaryGeometry = CalculateGroupBoundary(plotsForGroup3);
            var group3Boundary = ConvertToPolygon(group3BoundaryGeometry);
            var group3TotalArea = plotsForGroup3.Sum(p => p.Area);

            var groupsToSeed = new List<Group>
            {
                new Group
                {
                    ClusterId = cluster1.Id,
                    SupervisorId = supervisor1.Id,
                    RiceVarietyId = st25Variety.Id,
                    SeasonId = heThuSeason.Id,
                    PlantingDate = new DateTime(2025, 12, 30, 0, 0, 0, DateTimeKind.Utc),
                    Status = GroupStatus.Active,
                    Plots = plotsForGroup1,
                    TotalArea = group1TotalArea,
                    Area = group1Boundary
                },

                new Group
            {
                ClusterId = cluster1.Id,
                SupervisorId = supervisor1.Id,
                RiceVarietyId = st25Variety.Id,
                SeasonId = heThuSeason.Id,
                PlantingDate = new DateTime(2025, 12, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = GroupStatus.Active,
                Plots = plotsForGroup2,
                TotalArea = group2TotalArea,
                Area = group2Boundary
            },
                new Group
            {
                ClusterId = cluster2.Id,
                SupervisorId = supervisor2.Id,
                RiceVarietyId = st25Variety.Id,
                SeasonId = heThuSeason.Id,
                PlantingDate = new DateTime(2025, 12, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = GroupStatus.Active,
                Plots = plotsForGroup3,
                TotalArea = group3TotalArea,
                Area = group3Boundary
            }
            };
            await _context.Groups.AddRangeAsync(groupsToSeed);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully seeded {Count} Groups.", groupsToSeed.Count);
        }
        private async Task SeedClusterAsync()
        {
            if (await _context.Clusters.AnyAsync())
            {
                _logger.LogInformation("Cluster data already exists. Skipping seeding.");
                return;
            }
            var clusterManager1 = await _userManager.FindByEmailAsync("cluster1@ricepro.com") as ClusterManager;
            var clusterManager2 = await _userManager.FindByEmailAsync("cluster2@ricepro.com") as ClusterManager;

            if (clusterManager1 == null || clusterManager2 == null)
            {
                _logger.LogWarning("One or more Cluster Managers not found. Skipping Cluster seeding. Ensure users are seeded first.");
                return;
            }

            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var clustersToSeed = new List<Cluster>
            {
        new Cluster
        {
            ClusterName = "DongThap1",
            ClusterManagerId = clusterManager1.Id,
            Area = 150.75m,
            Boundary = geometryFactory.CreatePolygon(new Coordinate[]
            {
                new Coordinate(105.70, 10.00), // Tọa độ góc 1
                new Coordinate(105.70, 10.15), // Tọa độ góc 2
                new Coordinate(105.85, 10.15), // Tọa độ góc 3
                new Coordinate(105.85, 10.00), // Tọa độ góc 4
                new Coordinate(105.70, 10.00)  // Quay về điểm đầu để khép kín vùng
            })
        },
            new Cluster
        {
            ClusterName = "AnGiang2",
            ClusterManagerId = clusterManager2.Id,
            Area = 220.50m,
            Boundary = geometryFactory.CreatePolygon(new Coordinate[]
            {
                new Coordinate(105.40, 10.30),
                new Coordinate(105.40, 10.45),
                new Coordinate(105.55, 10.45),
                new Coordinate(105.55, 10.30),
                new Coordinate(105.40, 10.30)
            })
        }
        };
            await _context.Clusters.AddRangeAsync(clustersToSeed);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} new clusters.", clustersToSeed.Count);
        }
        private async Task SeedPlotDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
            var farmerUser = await _userManager.FindByEmailAsync("farmer1@ricepro.com") as Farmer;
            var farmerUser2 = await _userManager.FindByEmailAsync("farmer2@ricepro.com") as Farmer;
            var farmerUser3 = await _userManager.FindByEmailAsync("farmer3@ricepro.com") as Farmer;
            if (farmerUser == null)
            {
                _logger.LogWarning("Farmer 'farmer1@ricepro.com' could not found. Skipping plot seeding. Ensure users are seeded first");
                return;
            }

            if (farmerUser2 == null)
            {
                _logger.LogWarning("Farmer 'farmer2@ricepro.com' could not found. Skipping plot seeding. Ensure users are seeded first");
                return;
            }

            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

            if (!_context.Plots.Any())
            {
                var plots = new List<Plot>
                {
                    new Plot {
                        FarmerId = farmerUser.Id,
                        Boundary = geometryFactory.CreatePolygon(new Coordinate[]
                        {
                            new Coordinate(105.700, 10.000),    // SW corner
                            new Coordinate(105.700, 10.005),    // NW corner
                            new Coordinate(105.708, 10.005),    // NE corner
                            new Coordinate(105.708, 10.000),    // SE corner
                            new Coordinate(105.700, 10.000)     // Back to SW corner (close the ring)
                        }),
                        SoThua = 15,
                        SoTo = 36,
                        Area = 5.5m,
                        SoilType = "Đất phù sa",
                        Coordinate = geometryFactory.CreatePoint(new Coordinate(105.704, 10.0025)),
                        Status = PlotStatus.Active,
                    },

                    new Plot
                    {
                        FarmerId = farmerUser2.Id,
                        Boundary = geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(105.800, 10.100),
                        new Coordinate(105.800, 10.110),
                        new Coordinate(105.815, 10.110),
                        new Coordinate(105.815, 10.100),
                        new Coordinate(105.800, 10.100)
                        }),
                        SoThua = 18,
                        SoTo = 12,
                        Area = 12,
                        SoilType = "Đất phù sa",
                        Coordinate = geometryFactory.CreatePoint(new Coordinate(105.8075, 10.105)),
                        Status = PlotStatus.Active
                    },
                    new Plot
                    {
                        FarmerId = farmerUser3.Id,
                    Boundary = geometryFactory.CreatePolygon(new Coordinate[]
                        {
                            new Coordinate(11.210168500427, 106.42701488353),
                            new Coordinate(11.20919692067,  106.42252632102),
                            new Coordinate(11.213582994862, 106.42153433778),
                            new Coordinate(11.214553605806, 106.42601646253),
                            new Coordinate(11.210168500427, 106.42701488353),
                        }),
                        SoThua = 16,
                        SoTo = 58,
                        Area =  25.0857m,
                        SoilType = "Đất nông nghiệp",
                        Coordinate = geometryFactory.CreatePoint(new Coordinate(11.211290, 106.425131)),
                        Status = PlotStatus.Active
                    },
                    new Plot
                    {
                        FarmerId = farmerUser3.Id,
                        Boundary = geometryFactory.CreatePolygon(new Coordinate[]
                        {
                            new Coordinate(11.215557861556,  106.4305890557),
                            new Coordinate(11.211197392783,  106.43148705062),
                            new Coordinate(11.211014284582,  106.43080189246),
                            new Coordinate(11.210198687479,  106.42708240463),
                            new Coordinate(11.214569243013,  106.42608561738),
                            new Coordinate(11.215557861556,  106.4305890557),
                        }),
                        SoThua = 17,
                        SoTo = 58,
                        Area =  25.0857m,
                        SoilType = "Đất nông nghiệp",
                        Coordinate = geometryFactory.CreatePoint(new Coordinate(11.212688, 106.427436)),
                        Status = PlotStatus.Active
                    }

                };
                await _context.AddRangeAsync(plots);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} plots for Farmer {FarmerEmail}.");
            }
            else
            {
                _logger.LogInformation("Plots already exist. Skipping plot seeding.");
            }

            _logger.LogInformation("Core data seeding completed. 🌾");
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
        private async Task SeedRiceVarietyCategoriesAsync()
        {
            if (!_context.RiceVarietyCategories.Any())
            {
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
            else
            {
                _logger.LogInformation("Rice variety categories already exist - skipping seeding");
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

            var shortCategory = await _context.RiceVarietyCategories
                .FirstOrDefaultAsync(c => c.CategoryCode == "short");
            var longCategory = await _context.RiceVarietyCategories
                .FirstOrDefaultAsync(c => c.CategoryCode == "long");

            if (shortCategory == null || longCategory == null)
            {
                _logger.LogError("Rice variety categories not found. Ensure categories are seeded first.");
                return;
            }

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
                    var categoryId = data.Duration < 100 ? shortCategory.Id : longCategory.Id;
                    
                    _context.RiceVarieties.Add(new RiceVariety
                    {
                        VarietyName = data.Name,
                        CategoryId = categoryId,
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
                IsActive = true,
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
                AmmountPerMaterial = 70,
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
                var currentDate = DateTime.UtcNow;
                var materialPrices = new List<MaterialPrice>
                {
                    new MaterialPrice
                    {
                        MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                        PricePerMaterial = 345000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("98AB7097-ECC9-444B-A9A2-26207E28E679"),
                        PricePerMaterial = 750000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"),
                        PricePerMaterial = 896500,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0"),
                        PricePerMaterial = 814500,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("1385516C-B4A3-4F62-9D4D-D55BFB484C47"),
                        PricePerMaterial = 36000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("05949927-5F48-4955-A9A1-6B15E525E8E7"),
                        PricePerMaterial = 66000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5"),
                        PricePerMaterial = 107000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"),
                        PricePerMaterial = 100000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("4DBE9AC3-4900-4919-B55D-9607F36490D2"),
                        PricePerMaterial = 219000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"),
                        PricePerMaterial = 100000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1"),
                        PricePerMaterial = 194000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"),
                        PricePerMaterial = 86000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("DB1BB9F3-34FE-419C-860A-99DBEDB69092"),
                        PricePerMaterial = 314000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"),
                        PricePerMaterial = 0,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"),
                        PricePerMaterial = 299000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"),
                        PricePerMaterial = 25000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2"),
                        PricePerMaterial = 90000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"),
                        PricePerMaterial = 96000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"),
                        PricePerMaterial = 219000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5"),
                        PricePerMaterial = 288000,
                        ValidFrom = currentDate
                    },
                    new MaterialPrice
                    {
                        MaterialId = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD"),
                        PricePerMaterial = 26000,
                        ValidFrom = currentDate
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
        private async Task SeedStandardPlanDataAsync()
        {
            if (!_context.Set<StandardPlan>().Any(p => p.PlanName.Contains("Vụ")))
            {
                var longCategory = await _context.RiceVarietyCategories
                    .FirstOrDefaultAsync(c => c.CategoryCode == "long");
                
                if (longCategory == null)
                {
                    _logger.LogError("Long category not found. Ensure categories are seeded first.");
                    return;
                }

                var st25Variety = await _context.Set<RiceVariety>().FirstOrDefaultAsync(v => v.VarietyName == "ST25");
                if (st25Variety == null)
                {
                    st25Variety = new RiceVariety
                    {
                        Id = new Guid("00000000-0000-0000-0000-000000000001"),
                        VarietyName = "ST25",
                        CategoryId = longCategory.Id,
                        Description = "Lúa ST25 - Giống lúa chất lượng cao Việt Nam.",
                        IsActive = true
                    };
                    await _context.Set<RiceVariety>().AddAsync(st25Variety);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Seeded ST25 RiceVariety");
                }

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

                var seasonsData = new (string Name, string Type, string StartDate, string EndDate, string SowingDate)[]
                {
                    ("Đông Xuân", "Winter-Spring", "20/12", "04/03", "19/12"),
                    ("Hè Thu", "Summer-Autumn", "15/05", "04/08", "14/05"),
                    ("Thu Đông", "Autumn-Winter", "10/09", "30/11", "09/09")
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
                        CategoryId = longCategory.Id,
                        ExpertId = expertId,
                        PlanName = $"Quy Trình Canh Tác - Vụ {season.Name} (Giống dài ngày)",
                        Description = $"Quy trình sản xuất lúa cho vụ {season.Name} với ngày gieo sạ {season.StartDate}. Mùa: {season.Type}, Thời gian vụ: {season.StartDate} đến {season.EndDate}.",
                        TotalDurationDays = 81,
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
                            StageName = "Làm đất bón lót",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 1,
                            SequenceOrder = 1,
                            IsMandatory = true,
                            Notes = "Chuẩn bị hạt giống bón phân cho đất trước khi sạ."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Sạ hàng",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 1,
                            SequenceOrder = 2,
                            IsMandatory = true,
                            Notes = "Gieo để hạt giống đều và giữ độ ẩm phù hợp để cây mọc mầm."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Chăm sóc sau sạ",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 15,
                            SequenceOrder = 3,
                            IsMandatory = true,
                            Notes = "Chăm sóc ngay sau sạ, bao gồm trừ sâu bệnh và bón phân đầu."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Chăm sóc đẻ nhánh",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 20,
                            SequenceOrder = 4,
                            IsMandatory = true,
                            Notes = "Giai đoạn đẻ nhánh, kiểm soát nước và dinh dưỡng."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Chăm sóc vươn lóng đến trỗ",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 30,
                            SequenceOrder = 5,
                            IsMandatory = true,
                            Notes = "Từ vươn lóng đến trỗ bông, bón thúc và phòng trừ."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Chăm sóc trỗ đến chín",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 25,
                            SequenceOrder = 6,
                            IsMandatory = true,
                            Notes = "Từ trỗ đến chín hạt, tập trung phòng sâu bệnh."
                        },
                        new StandardPlanStage
                        {
                            Id = Guid.NewGuid(),
                            StageName = "Thu hoạch lúa và bảo quản",
                            StandardPlanId = seasonalPlan.Id,
                            ExpectedDurationDays = 7,
                            SequenceOrder = 7,
                            IsMandatory = true,
                            Notes = "Thu hoạch và bảo quản sau khi chín."
                        }
                    };

                    allStages.AddRange(stages);


                    // Tasks based on Excel rows (DaysAfter from "Ngày sau sạ", common across seasons)
                    var tasks = new List<StandardPlanTask>();

                    // Stage 1: Trước sạ (-1)
                    var stage1 = stages.First(s => s.SequenceOrder == 1);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage1.Id,
                        TaskName = "Bón lót",
                        Description = "- Bón lót các loại phân như phân hữu cơ, lân để sau khi sạ cây mọc mầm có thể cung cấp dinh dưỡng\r\n- Bón trước khi bừa trục và trạc",
                        DaysAfter = -1,
                        DurationDays = 1,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage1.Id,
                        TaskName = "Làm đất",
                        Description = "- Cày bừa lại theo phương pháp bừa trục và trạc để san phẳng mặt ruộng hạn chế chênh lệch tối đa các vùng cao thấp không quá 5cm\r\n- Kết hợp xử lý cỏ dại ven bờ, đánh rãnh để thoát phèn và diệt ốc",
                        DaysAfter = -1,
                        DurationDays = 0,
                        TaskType = TaskType.Sowing,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });

                    // Stage 2: Sạ hàng (0)
                    var stage2 = stages.First(s => s.SequenceOrder == 2);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage2.Id,
                        TaskName = "Sạ (ngày 0)",
                        Description = "Gieo để hạt giống đều và giữ độ ẩm phù hợp để cây mọc mầm",
                        DaysAfter = 0,
                        DurationDays = 1,
                        TaskType = TaskType.Sowing,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });

                    // Stage 3: Chăm sóc sau sạ (0-2, 4-7, 15-18)
                    var stage3 = stages.First(s => s.SequenceOrder == 3);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Phòng trừ dịch hại (ốc) (ngày 1-2)",
                        Description = "Sau sạ tiến hành xử lý ốc để khi cây lúa mọc mầm không bị ốc gây hại hỏng cây",
                        DaysAfter = 0,
                        DurationDays = 2,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Phòng trừ dịch hại (cỏ - mầm cỏ) (ngày 2-4)",
                        Description = "- Tiến hành xử lý cỏ dại để tránh cạnh tranh dinh dưỡng với cây lúa khi mọc mầm \r\n- Sử dụng các loại thuốc trừ cỏ tiền này mầm",
                        DaysAfter = 2,
                        DurationDays = 2,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Bơm nước (ngày 4-15)",
                        Description = "Sau sạ 4 ngày thì tến hành bơm nước vào ruộng.",
                        DaysAfter = 4,
                        DurationDays = 11,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 3
                    });
                    tasks.Add(new StandardPlanTask
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
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage3.Id,
                        TaskName = "Bón thúc lần 1 (ngày 15-18)",
                        Description = "Thực hiện bón lót lần 1 cho cây lúa lần đầu để khi cây con được cung cấp đầy đủ dinh dưỡng cho phát triển",
                        DaysAfter = 15,
                        DurationDays = 5,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 5
                    });

                    // Stage 4: Chăm sóc đẻ nhánh (20-22, 30-35, 35-38, 35-42, 39-46)
                    var stage4 = stages.First(s => s.SequenceOrder == 4);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Phòng trừ dịch hại (ngày 20-22)",
                        Description = "Sau sạ 20-22 ngày cần tiến hành kiểm tra đồng ruộng để phòng trừ sâu bệnh gây hại",
                        DaysAfter = 20,
                        DurationDays = 10,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Bón thúc lần 2 (ngày 30-35)",
                        Description = "Bón thúc lần 2 bổ sung đạm và lân cho cây lúa sinh trưởng và chuẩn bị bước vào thời kỳ đẻ nhánh giúp đẻ nhánh nhiều, tập trung và rảnh khỏe",
                        DaysAfter = 30,
                        DurationDays = 5,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.Normal,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Phòng trừ dịch hại (ngày 35-38)",
                        Description = "- Sau sạ 35-38 ngày tiến hành kiểm tra đồng ruộng để phòng trừ nấm, sâu gây hại trên lúa\r\n- Giai đoạn này nếu cây lúa bị ảnh hưởng sẽ gây thiệt hại rất lớn, làm lúa chậm phát triển",
                        DaysAfter = 35,
                        DurationDays = 15,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 3
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Rút nước (ngày 35-42)",
                        Description = "- Tháo cạn nước khô ruộng để hạn chế đẻ nhánh vô hiệu cho lúa\r\n- Thời gian giữ ruộng khô 4-7 ngày sau đó lại đưa nước vào ruộng để duy trì độ ẩm cho lúa",
                        DaysAfter = 35,
                        DurationDays = 4,
                        TaskType = TaskType.Sowing,
                        Priority = TaskPriority.High,
                        SequenceOrder = 4
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage4.Id,
                        TaskName = "Bơm nước (ngày 35-39)",
                        Description = "- Sau khi kết thúc thời kỳ đẻ nhánh thì tiếp tục cho nước vào để duy trì độ ẩm thường xuyên cho lúa\r\n- Thời kỳ này kéo dài 11-15 ngày",
                        DaysAfter = 35,
                        DurationDays = 11,
                        TaskType = TaskType.Sowing,
                        Priority = TaskPriority.High,
                        SequenceOrder = 5
                    });

                    // Stage 5: Chăm sóc vươn lóng đến trỗ (50-55, 55-60)
                    var stage5 = stages.First(s => s.SequenceOrder == 5);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Bón thúc lần 3 (ngày 50-55)",
                        Description = "- Bón thúc lần 3 bổ sung đạm và kali cho cây lúa sinh trưởng và chuẩn bị bước vào thời kỳ làm đóng đến chỗ\r\n- Giai đoạn này rất quan trọng với cây lúa sau thời gian đẻ nhánh cây cần lượng dinh dưỡng để các nhánh phát triển",
                        DaysAfter = 50,
                        DurationDays = 5,
                        TaskType = TaskType.Fertilization,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Phòng trừ dịch hại (ngày 55-60)",
                        Description = "Sau sạ 55-60 ngày (sau bón thúc lần 3) tiến hành kiểm tra ruộng lúa đánh giá phòng trừ sâu và đạo ôn, khuẩn trên lá",
                        DaysAfter = 55,
                        DurationDays = 5,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Rút nước (sau vươn đòng)",
                        Description = "Sau thời kỳ vươn đòng thì tiếp tục điều tiết ruộng khô bằng cách rút hết nước",
                        DaysAfter = 55,
                        DurationDays = 3,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 3
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage5.Id,
                        TaskName = "Bơm nước (sau rút nước 3-4 ngày)",
                        Description = "Sau khi rút khô từ 3-4 ngày thì tiếp tục cho nước vào để cây lúa đủ ẩm để cây sinh trưởng phát triển",
                        DaysAfter = 58,
                        DurationDays = 2,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 4
                    });

                    // Stage 6: Chăm sóc trỗ đến chín (~60-65, ~70, ~75-80)
                    var stage6 = stages.First(s => s.SequenceOrder == 6);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage6.Id,
                        TaskName = "Phòng trừ dịch hại (ngày 60-65)",
                        Description = "- Sau khi lúa bắt đầu trổ lẹt xẹt (60-65) tiến hành kiểm tra đồng ruồng để phòng trừ sâu rầy và bệnh gây hại",
                        DaysAfter = 60,
                        DurationDays = 20,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage6.Id,
                        TaskName = "Phòng trừ dịch hại (ngày 80-90)",
                        Description = "- Sau khi lúa bắt đầu cong trái me (80-90) tiến hành kiểm tra đồng ruồng để phòng trừ sâu rầy và bệnh gây hại",
                        DaysAfter = 80,
                        DurationDays = 1,
                        TaskType = TaskType.PestControl,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
                    });

                    // Stage 7: Thu hoạch lúa và bảo quản (~90+)
                    var stage7 = stages.First(s => s.SequenceOrder == 7);
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage7.Id,
                        TaskName = "Rút nước",
                        Description = "- Bước vào giai đoạn chín sáp thì bắt đầu rút cạn nước để thuận lợi cho quá trình thu hoạch",
                        DaysAfter = 90,
                        DurationDays = 7,
                        TaskType = TaskType.Harvesting,
                        Priority = TaskPriority.High,
                        SequenceOrder = 1
                    });
                    tasks.Add(new StandardPlanTask
                    {
                        Id = Guid.NewGuid(),
                        StandardProductionStageId = stage7.Id,
                        TaskName = "Thu hoạch",
                        Description = "- Tiến hành thu hoạch lúa sau khi lúa đã chín hoàn toàn\r\n- Sử dụng máy và các công cụ cần thiết phục vụ cho thu hoạch",
                        DaysAfter = 97,
                        DurationDays = 7,
                        TaskType = TaskType.Harvesting,
                        Priority = TaskPriority.High,
                        SequenceOrder = 2
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

                    // Diệt ốc: Ốc ôm (Niclosamide: 700g/kg) Sạch Ốc 3.6_400ml ( Abamectin 3.6g/ lít)
                    var dietOcTask = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ốc) (ngày 1-2)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = dietOcTask.Id,
                        MaterialId = new Guid("1385516C-B4A3-4F62-9D4D-D55BFB484C47"), // Ốc ôm (Niclosamide: 700g/kg )
                        QuantityPerHa = 700.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = dietOcTask.Id,
                        MaterialId = new Guid("05949927-5F48-4955-A9A1-6B15E525E8E7"), // Sạch Ốc 3.6_400ml ( Abamectin 3.6g/ lít)
                        QuantityPerHa = 2000.0m
                    });

                    // Phòng trừ dịch hại: Cỏ + mầm cỏ: Butaco + Cantanil (1350 ml + 1440 ml)
                    var coHauNayTask = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (cỏ - mầm cỏ) (ngày 2-4)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = coHauNayTask.Id,
                        MaterialId = new Guid("9E524C9B-2BFE-444F-AAA1-6D16C36BDC6B"), // Butaco
                        QuantityPerHa = 1350.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = coHauNayTask.Id,
                        MaterialId = new Guid("4B331200-E729-412C-AE0C-4484A3E6EEA5"), // Cantanil
                        QuantityPerHa = 1440.0m
                    });

                    // Bón sau sạ: Ure 50 kg/ha
                    var sauSaTask = tasks.First(t => t.TaskName == "Bón sau sạ (ngày 5-7)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = sauSaTask.Id,
                        MaterialId = new Guid("98AB7097-ECC9-444B-A9A2-26207E28E679"), // Phân Ure
                        QuantityPerHa = 50.000m
                    });

                    // Bón thúc 1: NPK 22-15-5 +1S 100 kg/ha
                    var thuc1Task = tasks.First(t => t.TaskName == "Bón thúc lần 1 (ngày 15-18)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc1Task.Id,
                        MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"), // Lúa Xanh Bón Thúc
                        QuantityPerHa = 100.000m
                    });

                    // Phòng trừ 15-18: Amino Gold + Villa Fuji + DT Aba (quantities from previous/approx)
                    var phongTru20Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ngày 20-22)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru20Task.Id,
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), // Amino Gold
                        QuantityPerHa = 500.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru20Task.Id,
                        MaterialId = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"), // Villa Fuji
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru20Task.Id,
                        MaterialId = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1"), // DT Aba
                        QuantityPerHa = 480.0m
                    });

                    // Bón thúc 2: NPK 22-15-5 100-150 kg/ha (150 usually)
                    var thuc2Task = tasks.First(t => t.TaskName == "Bón thúc lần 2 (ngày 30-35)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc2Task.Id,
                        MaterialId = new Guid("A575B22D-053D-440E-BCC5-F152F11C8A22"),
                        QuantityPerHa = 150.000m
                    });

                    // Phòng trừ 35-38: DT11 + DT Ema + Villa Fuji + Rusem super
                    var phongTru35Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ngày 35-38)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru35Task.Id,
                        MaterialId = new Guid("FCCD3DE6-B604-41C6-9D23-66F071CA7319"), // DT11
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru35Task.Id,
                        MaterialId = new Guid("DB1BB9F3-34FE-419C-860A-99DBEDB69092"), // DT Ema
                        QuantityPerHa = 480m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru35Task.Id,
                        MaterialId = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"), // Villa Fuji
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru35Task.Id,
                        MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"), // Rusem super
                        QuantityPerHa = 75.0m
                    });

                    // Bón thúc 3: NPK 15-5-20+1S 100-150 kg/ha (150 usually)
                    var thuc3Task = tasks.First(t => t.TaskName == "Bón thúc lần 3 (ngày 50-55)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = thuc3Task.Id,
                        MaterialId = new Guid("2167503B-F6D3-4E87-B426-0FE78ADDDCA0"), // Lúa Vàng Bón Đòng 15-5-20+ 1S
                        QuantityPerHa = 150.000m
                    });

                    // Phòng trừ 55-60: DT Aba + Upper 400SC + Captival + DT11 + Rusem super
                    var phongTru55Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ngày 55-60)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru55Task.Id,
                        MaterialId = new Guid("1C62D597-86EA-4B9F-8F67-8FEC5BA386B1"), // DT Aba
                        QuantityPerHa = 480.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru55Task.Id,
                        MaterialId = new Guid("58200EA8-3B9B-4B13-B841-5D7D7917A95C"), // Upper 400SC
                        QuantityPerHa = 360.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru55Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 125.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru55Task.Id,
                        MaterialId = new Guid("5AF3EB7B-E068-4FFF-97B8-12291D18A0D2"), // DT11 Đồng to
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru55Task.Id,
                        MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"), // Rusem super
                        QuantityPerHa = 75.0m
                    });

                    // Phòng trừ 60-65: Captival + Villa Fuji + DT9 Vua vào gạo + Amino Gold + Trắng xanh WP + Rusem super
                    var phongTru60Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ngày 60-65)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 125.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("3BE50B7F-55DC-4E3C-9686-04664BCABA14"), // Villa Fuji
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("60061BBE-1DCA-48B1-B291-41497D3BAE76"), // DT9 Vua vào gạo
                        QuantityPerHa = 1000.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), // Amino Gold
                        QuantityPerHa = 500.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5"), // Trắng xanh WP
                        QuantityPerHa = 1.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru60Task.Id,
                        MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"), // Rusem super
                        QuantityPerHa = 75.0m
                    });

                    // Phòng trừ 80: Captival + Trắng xanh WP + Amino Gold + Rusem super + DT 6
                    var phongTru80Task = tasks.First(t => t.TaskName == "Phòng trừ dịch hại (ngày 80-90)");
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru80Task.Id,
                        MaterialId = new Guid("56B90D7A-9671-40C4-B36B-24621DEEFED0"), // Captival
                        QuantityPerHa = 125.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru80Task.Id,
                        MaterialId = new Guid("DC92CDEE-7D8B-4C43-9586-8DE46B1BE8B5"), // Trắng xanh WP
                        QuantityPerHa = 1.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru80Task.Id,
                        MaterialId = new Guid("5731730F-B20E-4309-9A0B-0A36B40AEBD0"), // Amino Gold
                        QuantityPerHa = 500.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru80Task.Id,
                        MaterialId = new Guid("6D33769E-8099-4A10-8B86-B20DCC1CC545"), // Rusem super
                        QuantityPerHa = 75.0m
                    });
                    allTaskMaterials.Add(new StandardPlanTaskMaterial
                    {
                        Id = Guid.NewGuid(),
                        StandardPlanTaskId = phongTru80Task.Id,
                        MaterialId = new Guid("11FB236B-AA4D-46F6-9461-FE4EB810E5CD"), // DT6
                        QuantityPerHa = 1000.0m
                    });

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
                var farmer1Id = _userManager.FindByEmailAsync("farmer1@ricepro.com").Result?.Id ?? Guid.NewGuid();
                var farmer2Id = _userManager.FindByEmailAsync("farmer2@ricepro.com").Result?.Id ?? Guid.NewGuid();
                var farmer3Id = _userManager.FindByEmailAsync("farmer3@ricepro.com").Result?.Id ?? Guid.NewGuid();
                var farmer4Id = _userManager.FindByEmailAsync("farmer4@ricepro.com").Result?.Id ?? Guid.NewGuid();

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
                LastModified = DateTime.UtcNow,
                Plots = new List<Plot>
                {
                    new Plot
                    {
                        Id = new Guid("F9023C7B-B4EE-4D58-8B46-6AC9AB415FF7"),
                        FarmerId = farmer1Id,
                        Area =  3.00m,
                        Boundary = polygonGroup1 // Có thể thêm Boundary nếu cần
                    },
                    new Plot
                    {
                        Id = new Guid("9901619B-9517-4DDB-80BC-6CCBA8EED484"),
                        FarmerId = farmer2Id,
                        Area =  6.50m,
                        Boundary = polygonGroup3 // Có thể thêm Boundary nếu cần
                    },
                    new Plot
                    {
                        Id = new Guid("947C9E3E-0F9B-40F3-ADFF-A74B7F70C8CC"),
                        FarmerId = farmer3Id,
                        Area =  3.50m,
                        Boundary = polygonCluster1 // Có thể thêm Boundary nếu cần
                    },
                    new Plot
                    {
                        Id = new Guid("96B35D4D-72C7-4CDE-A232-A59BA5B11E0B"),
                        FarmerId = farmer4Id,
                        Area =  10.00m,
                        Boundary = polygonCluster2 // Có thể thêm Boundary nếu cần
                    },
                }
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
                clusterManager1.ClusterId = cluster1Id;
                clusterManager2.ClusterId = cluster2Id;
                _context.Set<ClusterManager>().Update(clusterManager1);
                _context.Set<ClusterManager>().Update(clusterManager2);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} Groups", groups.Count);
            }
            else
            {
                _logger.LogInformation("Cluster data already exists - skipping seeding");
            }
            _logger.LogInformation("Core data seeding completed");
        }
        private async Task SeedProductionPlanAsync()
        {
            // Kiểm tra xem dữ liệu ProductionPlan đã được thêm chưa
            if (!_context.Set<ProductionPlan>().Any())
            {

                _logger.LogInformation("Seeding Core Data: Production Plans...");
                // Lấy Group và StandardPlan đã seed trước
                var group1 = await _context.Set<Group>()
                    .FirstOrDefaultAsync(g => g.Id == new Guid("67B40A3C-4C9D-4F7F-9A52-E23B9B42B101")); // Group 1 trong Cluster 1
                var group2 = await _context.Set<Group>()
                    .FirstOrDefaultAsync(g => g.Id == new Guid("3E8F5D2B-8A1C-4E7A-A1B9-F9C3A021E202")); // Group 2 trong Cluster 1
                var standardPlanDX = await _context.Set<StandardPlan>()
                    .FirstOrDefaultAsync(sp => sp.PlanName.Contains("Đông Xuân"));
                var standardPlanHT = await _context.Set<StandardPlan>()
                    .FirstOrDefaultAsync(sp => sp.PlanName.Contains("Hè Thu"));
                // Kiểm tra điều kiện cần thiết
                if (group1 == null || group2 == null || standardPlanDX == null || standardPlanHT == null)
                {
                    _logger.LogError("Required Groups or Standard Plans for Production Plan seeding not found. Skipping Production Plan seeding.");
                    return;
                }
                var productionPlanGuid1 = new Guid("E9C0A252-10B9-4190-96AC-4F1E19617CF5");
                var productionStageGuid1 = new Guid("86170DE5-672C-48B6-89EC-67113BDB1EBD");
                var productionPlanTaskGuid1 = new Guid("86170DE5-672C-48B6-89EC-67113BDB1EBD");

                var productionPlanGuid2 = new Guid("AD439C33-BAC6-4420-88D7-E81DA81C499A");

                var riceVarietyST25 = await _context.Set<RiceVariety>()
                    .FirstOrDefaultAsync(v => v.VarietyName == "ST25");

                var season = await _context.Set<Season>()
                    .FirstOrDefaultAsync(v => v.SeasonName == "Đông Xuân");
                // danh sách cultivation làm đất
                var cultivationTaskBonLotList = new List<CultivationTask>();
                foreach (var plot in group1.Plots)
                {
                    var cultivationTaskId = Guid.NewGuid();
                    cultivationTaskBonLotList.Add(new CultivationTask
                    {
                        Id = cultivationTaskId,
                        IsContingency = false,
                        ActualStartDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc),
                        ActualEndDate = DateTime.SpecifyKind(new DateTime(2024, 12, 20), DateTimeKind.Utc),
                        ActualMaterialCost = 2070000 * plot.Area,
                        ActualServiceCost = 7300000,
                        CompletedAt = DateTime.SpecifyKind(new DateTime(2024, 12, 20), DateTimeKind.Utc),
                        CultivationTaskMaterials = new List<CultivationTaskMaterial>()
                        {
                            new CultivationTaskMaterial
                            {
                                MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                                ActualQuantity = 300 * plot.Area,
                                ActualCost = 2070000 * plot.Area,
                                Notes = "- Bón phân lót bằng cách rải đều trên mặt ruộng kết hợp với bừa trục và trạc để vùi phân lót xuống dưới\r\n- Vật tư: Phân hữu cơ vi sinh \r\n- Liều lượng: 300kg/ha phân hữu cơ vi sinh "
                            }
                        },
                        PlotCultivation = new PlotCultivation
                        {
                            PlotId = plot.Id,
                            ActualYield = plot.Area,
                            PlantingDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc),
                            RiceVarietyId = riceVarietyST25.Id,
                            SeasonId = season.Id,
                            Status = CultivationStatus.Completed,
                        }
                    });
                }
                // Tạo các ProductionPlan mẫu
                var productionPlans = new List<ProductionPlan>
                {
                    new ProductionPlan
                    {
                        Id = productionPlanGuid1,
                        GroupId = group1.Id,
                        StandardPlanId = standardPlanDX.Id,
                        PlanName = "Vụ Đông Xuân 2024–2025",
                        BasePlantingDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc), // Dùng ngày nhóm sạ hoặc giả lập 30 ngày trước
                        Status = TaskStatus.Approved,
                        TotalArea = group1.TotalArea,
                        SubmittedAt = DateTime.SpecifyKind(new DateTime(2024, 12, 12), DateTimeKind.Utc),
                        ApprovedAt = DateTime.SpecifyKind(new DateTime(2024, 12, 15), DateTimeKind.Utc),
                        ApprovedBy = null, // Giả lập không có người duyệt cụm
                        SubmittedBy = group1.SupervisorId,
                        LastModified = DateTime.UtcNow,
                        CurrentProductionStages = new List<ProductionStage>()
                        {
                            new ProductionStage
                            {
                                  StageName = "Bón phân, làm đất",
                                  Description = "Giai đoạn trước khi vào công đoạn chăm sóc",
                                  IsActive = true,
                                  Notes = "Bón phân, làm đất trước sạ",
                                  SequenceOrder = 1,
                                  TypicalDurationDays = 1,
                                  ProductionPlanTasks = new List<ProductionPlanTask>()
                                  {
                                      new ProductionPlanTask
                                      {
                                          Priority = TaskPriority.High,
                                          SequenceOrder = 1,
                                          TaskName = "Bón lót",
                                          TaskType = TaskType.Fertilization,
                                          ScheduledDate = DateTime.SpecifyKind(new DateTime(2024, 12, 12), DateTimeKind.Utc),
                                          ScheduledEndDate = DateTime.SpecifyKind(new DateTime(2024, 12, 12).AddDays(1), DateTimeKind.Utc),
                                          Description = "- Bón lót các loại phân như phân hữu cơ, lân để sau khi sạ cây mọc mầm có thể cung cấp dinh dưỡng\r\n- Bón trước khi bừa trục và trạc",
                                          Status = TaskStatus.Completed,
                                          EstimatedMaterialCost = 2070000,
                                          ProductionPlanTaskMaterials = new List<ProductionPlanTaskMaterial>()
                                          {
                                              new ProductionPlanTaskMaterial
                                              {
                                                  MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                                                  QuantityPerHa = 300,
                                                  EstimatedAmount = 2070000
                                              }
                                          },
                                          CultivationTasks = cultivationTaskBonLotList
                                      },
                                      new ProductionPlanTask
                                      {
                                          Priority = TaskPriority.High,
                                          SequenceOrder = 2,
                                          TaskName = "Làm đất",
                                          TaskType = TaskType.Sowing,
                                          ScheduledDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc),
                                          ScheduledEndDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19).AddDays(1), DateTimeKind.Utc),
                                          Description = "- Cày bừa lại theo phương pháp bừa trục và trạc để san phẳng mặt ruộng hạn chế chênh lệch tối đa các vùng cao thấp không quá 5cm\r\n- Kết hợp xử lý cỏ dại ven bờ, đánh rãnh để thoát phèn và diệt ốc",
                                          Status = TaskStatus.Completed,
                                          ProductionPlanTaskMaterials = new List<ProductionPlanTaskMaterial>(),
                                      }
                                  }
                            },
                        }
                        },
                    new ProductionPlan
                    {
                        Id = productionPlanGuid2,
                        GroupId = group2.Id,
                        StandardPlanId = standardPlanDX.Id,
                        PlanName = "Vụ Đông Xuân 2023–2024",
                        BasePlantingDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc), // Dùng ngày nhóm sạ hoặc giả lập 30 ngày trước
                        Status = TaskStatus.PendingApproval,
                        TotalArea = group2.TotalArea,
                        SubmittedAt = DateTime.SpecifyKind(new DateTime(2024, 12, 12), DateTimeKind.Utc),
                        ApprovedAt = DateTime.SpecifyKind(new DateTime(2024, 12, 15), DateTimeKind.Utc),
                        ApprovedBy = null, // Giả lập không có người duyệt cụm
                        SubmittedBy = group2.SupervisorId,
                        LastModified = DateTime.UtcNow,
                        CurrentProductionStages = new List<ProductionStage>()
                        {
                            new ProductionStage
                            {
                                  StageName = "Bón phân, làm đất",
                                  Description = "Giai đoạn trước khi vào công đoạn chăm sóc",
                                  IsActive = true,
                                  Notes = "Bón phân, làm đất trước sạ",
                                  SequenceOrder = 1,
                                  TypicalDurationDays = 1,
                                  ProductionPlanTasks = new List<ProductionPlanTask>()
                                  {
                                      new ProductionPlanTask
                                      {
                                          Priority = TaskPriority.High,
                                          SequenceOrder = 1,
                                          TaskName = "Bón lót",
                                          TaskType = TaskType.Fertilization,
                                          ScheduledDate = DateTime.SpecifyKind(new DateTime(2024, 12, 12), DateTimeKind.Utc),
                                          ScheduledEndDate = DateTime.SpecifyKind(new DateTime(2024, 12, 12).AddDays(1), DateTimeKind.Utc),
                                          Description = "- Bón lót các loại phân như phân hữu cơ, lân để sau khi sạ cây mọc mầm có thể cung cấp dinh dưỡng\r\n- Bón trước khi bừa trục và trạc",
                                          Status = TaskStatus.Completed,
                                          EstimatedMaterialCost = 2070000,
                                          ProductionPlanTaskMaterials = new List<ProductionPlanTaskMaterial>()
                                          {
                                              new ProductionPlanTaskMaterial
                                              {
                                                  MaterialId = new Guid("1F25B94C-02A9-4558-BA4E-AD44CE155E49"),
                                                  QuantityPerHa = 300,
                                                  EstimatedAmount = 2070000
                                              }
                                          },
                                          CultivationTasks = cultivationTaskBonLotList
                                      },
                                      new ProductionPlanTask
                                      {
                                          Priority = TaskPriority.High,
                                          SequenceOrder = 2,
                                          TaskName = "Làm đất",
                                          TaskType = TaskType.Sowing,
                                          ScheduledDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19), DateTimeKind.Utc),
                                          ScheduledEndDate = DateTime.SpecifyKind(new DateTime(2024, 12, 19).AddDays(1), DateTimeKind.Utc),
                                          Description = "- Cày bừa lại theo phương pháp bừa trục và trạc để san phẳng mặt ruộng hạn chế chênh lệch tối đa các vùng cao thấp không quá 5cm\r\n- Kết hợp xử lý cỏ dại ven bờ, đánh rãnh để thoát phèn và diệt ốc",
                                          Status = TaskStatus.Completed,
                                          ProductionPlanTaskMaterials = new List<ProductionPlanTaskMaterial>(),
                                      }
                                  }
                            },
                        }
                        },
                    };
                await _context.Set<ProductionPlan>().AddRangeAsync(productionPlans);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} Production Plans", productionPlans.Count);

            }
            else
            {
                _logger.LogInformation("Production Plan data already exists - skipping seeding");
            }
            ;
        }
        private async Task SeedCoreDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
        private async Task SeedDataAsync()
        {
            _logger.LogInformation("Core data seeding completed");
        }
        private Polygon ConvertToPolygon(Geometry geometry)
        {
            if (geometry is Polygon polygon)
            {
                return polygon;
            }
            if (geometry is MultiPolygon multiPolygon)
            {
                return multiPolygon.Geometries
                    .Cast<Polygon>()
                    .OrderByDescending(p => p.Area)
                    .First();
            }
            if (geometry is GeometryCollection collection)
            {
                var firstPolygon = collection.Geometries
                    .OfType<Polygon>()
                    .FirstOrDefault();
            }

            return (Polygon)geometry.ConvexHull();

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
}