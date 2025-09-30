using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RiceProduction.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:alert_severity", "info,warning,critical,urgent")
                .Annotation("Npgsql:Enum:alert_source", "ai_weather,ai_pest,farmer_report,supervisor_inspection,system")
                .Annotation("Npgsql:Enum:alert_status", "new,acknowledged,in_progress,resolved,cancelled")
                .Annotation("Npgsql:Enum:cultivation_status", "planned,in_progress,completed,failed")
                .Annotation("Npgsql:Enum:group_status", "draft,active,ready_for_optimization,locked,exception")
                .Annotation("Npgsql:Enum:invoice_status", "draft,pending,approved,paid,cancelled")
                .Annotation("Npgsql:Enum:material_type", "fertilizer,pesticide,seed,other")
                .Annotation("Npgsql:Enum:plot_status", "active,inactive,emergency,locked")
                .Annotation("Npgsql:Enum:priority_level", "none,low,medium,high")
                .Annotation("Npgsql:Enum:risk_level", "low,medium,high")
                .Annotation("Npgsql:Enum:task_priority", "low,normal,high,critical,urgent")
                .Annotation("Npgsql:Enum:task_status", "draft,pending_approval,approved,in_progress,on_hold,completed,cancelled")
                .Annotation("Npgsql:Enum:task_type", "manual,uav_service")
                .Annotation("Npgsql:Enum:user_role", "admin,cluster_manager,supervisor,farmer,agronomy_expert,uav_vendor")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "ApplicationUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ClusterManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Boundary = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    Area = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiceVarieties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VarietyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BaseGrowthDurationDays = table.Column<int>(type: "integer", nullable: true, comment: "Base growth duration - actual duration may vary by season"),
                    BaseYieldPerHectare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, comment: "Base yield per hectare - actual yield may vary by season"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Characteristics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "General characteristics of this rice variety"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Whether this variety is currently active/available for planting"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiceVarieties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Type of season (e.g., Wet Season, Dry Season, Winter-Spring)"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Whether this season is currently active for planning"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.CheckConstraint("CK_Season_DateRange", "[EndDate] > [StartDate]");
                });

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Admins_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgronomyExperts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgronomyExperts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgronomyExperts_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Farmers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farmers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Farmers_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Supervisors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxFarmerCapacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentFarmerCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supervisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Supervisors_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UavVendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BusinessRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ServiceRatePerHa = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    CompletedServices = table.Column<int>(type: "integer", nullable: false),
                    FleetSize = table.Column<int>(type: "integer", nullable: false),
                    ServiceRadius = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    EquipmentSpecs = table.Column<string>(type: "jsonb", nullable: true),
                    OperatingSchedule = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UavVendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UavVendors_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_ApplicationUser_UserId",
                        column: x => x.UserId,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClusterManagers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterManagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterManagers_ApplicationUser_Id",
                        column: x => x.Id,
                        principalTable: "ApplicationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClusterManagers_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FieldWeathers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Humidity = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Rainfall = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    WindSpeed = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Conditions = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ForecastData = table.Column<string>(type: "jsonb", nullable: true),
                    AlertTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldWeathers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldWeathers_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaterialPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialPrices_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiceVarietySeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RiceVarietyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrowthDurationDays = table.Column<int>(type: "integer", nullable: false, comment: "Growth duration in days for this variety in this specific season"),
                    ExpectedYieldPerHectare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true, comment: "Expected yield per hectare for this variety-season combination"),
                    OptimalPlantingStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OptimalPlantingEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RiskLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Risk level: Low, Medium, or High"),
                    SeasonalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Special considerations for this variety-season combination"),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Whether this variety is recommended for this season"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiceVarietySeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiceVarietySeasons_RiceVarieties_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVarieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiceVarietySeasons_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StandardPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RiceVarietyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TotalDurationDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandardPlans_AgronomyExperts_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AgronomyExperts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StandardPlans_RiceVarieties_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVarieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RiceVarietyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlantingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsException = table.Column<bool>(type: "boolean", nullable: false),
                    ExceptionReason = table.Column<string>(type: "text", nullable: true),
                    ReadyForUavDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Area = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    TotalArea = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Groups_RiceVarieties_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVarieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Groups_Supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupervisorFarmerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AssignmentNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorFarmerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupervisorFarmerAssignments_Farmers_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "Farmers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupervisorFarmerAssignments_Supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StandardPlanTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DaysAfter = table.Column<int>(type: "integer", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    TaskType = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardPlanTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandardPlanTasks_StandardPlans_StandardPlanId",
                        column: x => x.StandardPlanId,
                        principalTable: "StandardPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Plots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FarmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Boundary = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: false),
                    Area = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    SoilType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Coordinate = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plots_Farmers_FarmerId",
                        column: x => x.FarmerId,
                        principalTable: "Farmers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Plots_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UavServiceOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UavVendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TotalArea = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TotalPlots = table.Column<int>(type: "integer", nullable: false),
                    OptimizedRoute = table.Column<LineString>(type: "geometry(LineString,4326)", nullable: true),
                    RouteData = table.Column<string>(type: "jsonb", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ActualCost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UavServiceOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UavServiceOrders_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UavServiceOrders_Supervisors_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UavServiceOrders_UavVendors_UavVendorId",
                        column: x => x.UavVendorId,
                        principalTable: "UavVendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StandardPlanTaskMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardPlanTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityPerHa = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardPlanTaskMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandardPlanTaskMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StandardPlanTaskMaterials_StandardPlanTasks_StandardPlanTas~",
                        column: x => x.StandardPlanTaskId,
                        principalTable: "StandardPlanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlotCultivations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiceVarietyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualYield = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Planned"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotCultivations", x => x.Id);
                    table.CheckConstraint("CK_PlotCultivation_ActualYield", "[ActualYield] IS NULL OR [ActualYield] >= 0");
                    table.ForeignKey(
                        name: "FK_PlotCultivations_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlotCultivations_RiceVarieties_RiceVarietyId",
                        column: x => x.RiceVarietyId,
                        principalTable: "RiceVarieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotCultivations_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UavInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UavVendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UavServiceOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalArea = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    RatePerHa = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AttachmentUrls = table.Column<string[]>(type: "text[]", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UavInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UavInvoices_UavServiceOrders_UavServiceOrderId",
                        column: x => x.UavServiceOrderId,
                        principalTable: "UavServiceOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UavInvoices_UavVendors_UavVendorId",
                        column: x => x.UavVendorId,
                        principalTable: "UavVendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlotCultivationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BasePlantingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    TotalArea = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AgronomyExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlans", x => x.Id);
                    table.CheckConstraint("CK_ProductionPlan_ApprovalFlow", "([ApprovedAt] IS NULL AND [ApprovedBy] IS NULL) OR ([ApprovedAt] IS NOT NULL AND [ApprovedBy] IS NOT NULL)");
                    table.CheckConstraint("CK_ProductionPlan_SubmissionFlow", "([SubmittedAt] IS NULL AND [SubmittedBy] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedBy] IS NOT NULL)");
                    table.CheckConstraint("CK_ProductionPlan_TotalArea", "[TotalArea] IS NULL OR [TotalArea] > 0");
                    table.ForeignKey(
                        name: "FK_ProductionPlans_AgronomyExperts_AgronomyExpertId",
                        column: x => x.AgronomyExpertId,
                        principalTable: "AgronomyExperts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionPlans_AgronomyExperts_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "AgronomyExperts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionPlans_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionPlans_PlotCultivations_PlotCultivationId",
                        column: x => x.PlotCultivationId,
                        principalTable: "PlotCultivations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionPlans_StandardPlans_StandardPlanId",
                        column: x => x.StandardPlanId,
                        principalTable: "StandardPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionPlans_Supervisors_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlanTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardPlanTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TaskType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    EstimatedMaterialCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    EstimatedServiceCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlanTasks", x => x.Id);
                    table.CheckConstraint("CK_ProductionPlanTask_DateRange", "[ScheduledEndDate] IS NULL OR [ScheduledEndDate] >= [ScheduledDate]");
                    table.ForeignKey(
                        name: "FK_ProductionPlanTasks_ProductionPlans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "ProductionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionPlanTasks_StandardPlanTasks_StandardPlanTaskId",
                        column: x => x.StandardPlanTaskId,
                        principalTable: "StandardPlanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CultivationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionPlanTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotCultivationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToVendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionOrder = table.Column<int>(type: "integer", nullable: true),
                    IsContingency = table.Column<bool>(type: "boolean", nullable: false),
                    ContingencyReason = table.Column<string>(type: "text", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualMaterialCost = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ActualServiceCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    CompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WeatherConditions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InterruptionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: true),
                    UavVendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CultivationTasks", x => x.Id);
                    table.CheckConstraint("CK_CultivationTask_CompletionPercentage", "[CompletionPercentage] >= 0 AND [CompletionPercentage] <= 100");
                    table.CheckConstraint("CK_CultivationTask_DateRange", "[ActualEndDate] IS NULL OR [ActualEndDate] >= [ActualStartDate]");
                    table.CheckConstraint("CK_CultivationTask_NonNegativeCosts", "[ActualCost] >= 0 AND [ActualServiceCost] >= 0");
                    table.ForeignKey(
                        name: "FK_CultivationTasks_PlotCultivations_PlotCultivationId",
                        column: x => x.PlotCultivationId,
                        principalTable: "PlotCultivations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CultivationTasks_ProductionPlanTasks_ProductionPlanTaskId",
                        column: x => x.ProductionPlanTaskId,
                        principalTable: "ProductionPlanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CultivationTasks_Supervisors_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CultivationTasks_Supervisors_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Supervisors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CultivationTasks_Supervisors_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CultivationTasks_UavVendors_AssignedToVendorId",
                        column: x => x.AssignedToVendorId,
                        principalTable: "UavVendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CultivationTasks_UavVendors_UavVendorId",
                        column: x => x.UavVendorId,
                        principalTable: "UavVendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlanTaskMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionPlanTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityPerHa = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    MaterialId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlanTaskMaterials", x => x.Id);
                    table.CheckConstraint("CK_ProductionPlanTaskMaterial_NonNegativeCost", "[EstimatedCost] >= 0");
                    table.CheckConstraint("CK_ProductionPlanTaskMaterial_PositiveQuantity", "[EstimatedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_ProductionPlanTaskMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionPlanTaskMaterials_Materials_MaterialId1",
                        column: x => x.MaterialId1,
                        principalTable: "Materials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionPlanTaskMaterials_ProductionPlanTasks_ProductionP~",
                        column: x => x.ProductionPlanTaskId,
                        principalTable: "ProductionPlanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlertType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AiConfidence = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    AiThresholdExceeded = table.Column<bool>(type: "boolean", nullable: false),
                    AiRawData = table.Column<string>(type: "jsonb", nullable: true),
                    RecommendedAction = table.Column<string>(type: "text", nullable: true),
                    RecommendedMaterials = table.Column<string>(type: "jsonb", nullable: true),
                    RecommendedUrgencyHours = table.Column<int>(type: "integer", nullable: true),
                    NotifiedUsers = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedEmergencyTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgronomyExpertId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_AgronomyExperts_AgronomyExpertId",
                        column: x => x.AgronomyExpertId,
                        principalTable: "AgronomyExperts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Alerts_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_CultivationTasks_CreatedEmergencyTaskId",
                        column: x => x.CreatedEmergencyTaskId,
                        principalTable: "CultivationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Supervisors_AcknowledgedBy",
                        column: x => x.AcknowledgedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Supervisors_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CultivationTaskMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CultivationTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, comment: "Actual quantity of material used during task execution"),
                    ActualCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Actual cost incurred for this material"),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaterialId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CultivationTaskMaterials", x => x.Id);
                    table.CheckConstraint("CK_CultivationTaskMaterial_NonNegativeCost", "[ActualCost] >= 0");
                    table.CheckConstraint("CK_CultivationTaskMaterial_PositiveQuantity", "[ActualQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_CultivationTaskMaterials_CultivationTasks_CultivationTaskId",
                        column: x => x.CultivationTaskId,
                        principalTable: "CultivationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CultivationTaskMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CultivationTaskMaterials_Materials_MaterialId1",
                        column: x => x.MaterialId1,
                        principalTable: "Materials",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FarmLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CultivationTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlotCultivationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoggedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LoggedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WorkDescription = table.Column<string>(type: "text", nullable: true),
                    CompletionPercentage = table.Column<int>(type: "integer", nullable: false),
                    ActualAreaCovered = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    ActualMaterialJson = table.Column<string>(type: "jsonb", nullable: true),
                    ServiceCost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    ServiceNotes = table.Column<string>(type: "text", nullable: true),
                    PhotoUrls = table.Column<string[]>(type: "text[]", nullable: true),
                    WeatherConditions = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    InterruptionReason = table.Column<string>(type: "text", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmLogs_CultivationTasks_CultivationTaskId",
                        column: x => x.CultivationTaskId,
                        principalTable: "CultivationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmLogs_Farmers_LoggedBy",
                        column: x => x.LoggedBy,
                        principalTable: "Farmers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmLogs_PlotCultivations_PlotCultivationId",
                        column: x => x.PlotCultivationId,
                        principalTable: "PlotCultivations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmLogs_Supervisors_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "Supervisors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FarmLogMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FarmLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualQuantityUsed = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, comment: "Actual quantity of material used and recorded in this farm log entry"),
                    ActualCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Actual cost incurred for this material usage"),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmLogMaterials", x => x.Id);
                    table.CheckConstraint("CK_FarmLogMaterial_NonNegativeCost", "[ActualCost] >= 0");
                    table.CheckConstraint("CK_FarmLogMaterial_PositiveQuantity", "[ActualQuantityUsed] > 0");
                    table.ForeignKey(
                        name: "FK_FarmLogMaterials_FarmLogs_FarmLogId",
                        column: x => x.FarmLogId,
                        principalTable: "FarmLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FarmLogMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AcknowledgedBy",
                table: "Alerts",
                column: "AcknowledgedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AgronomyExpertId",
                table: "Alerts",
                column: "AgronomyExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AlertType",
                table: "Alerts",
                column: "AlertType");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ClusterId",
                table: "Alerts",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CreatedEmergencyTaskId",
                table: "Alerts",
                column: "CreatedEmergencyTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_GroupId",
                table: "Alerts",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_PlotId",
                table: "Alerts",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ResolvedBy",
                table: "Alerts",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Severity",
                table: "Alerts",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Source",
                table: "Alerts",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Status",
                table: "Alerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "ApplicationUser",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "ApplicationUser",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterManagers_ClusterId",
                table: "ClusterManagers",
                column: "ClusterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_Boundary",
                table: "Clusters",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_ClusterManagerId",
                table: "Clusters",
                column: "ClusterManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_ClusterName",
                table: "Clusters",
                column: "ClusterName");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTaskMaterial_MaterialId",
                table: "CultivationTaskMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTaskMaterial_Task_Material",
                table: "CultivationTaskMaterials",
                columns: new[] { "CultivationTaskId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTaskMaterial_TaskId",
                table: "CultivationTaskMaterials",
                column: "CultivationTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTaskMaterials_MaterialId1",
                table: "CultivationTaskMaterials",
                column: "MaterialId1");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTask_AssignedUser",
                table: "CultivationTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTask_AssignedVendor",
                table: "CultivationTasks",
                column: "AssignedToVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTask_PlotCultivationId",
                table: "CultivationTasks",
                column: "PlotCultivationId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTask_ProductionPlanTaskId",
                table: "CultivationTasks",
                column: "ProductionPlanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTasks_SupervisorId",
                table: "CultivationTasks",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTasks_UavVendorId",
                table: "CultivationTasks",
                column: "UavVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_CultivationTasks_VerifiedBy",
                table: "CultivationTasks",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Farmers_FarmCode",
                table: "Farmers",
                column: "FarmCode");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogMaterial_Log_Material",
                table: "FarmLogMaterials",
                columns: new[] { "FarmLogId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogMaterial_LogId",
                table: "FarmLogMaterials",
                column: "FarmLogId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogMaterial_MaterialId",
                table: "FarmLogMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogs_CultivationTaskId",
                table: "FarmLogs",
                column: "CultivationTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogs_LoggedBy",
                table: "FarmLogs",
                column: "LoggedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogs_PlotCultivationId",
                table: "FarmLogs",
                column: "PlotCultivationId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmLogs_VerifiedBy",
                table: "FarmLogs",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FieldWeathers_ClusterId",
                table: "FieldWeathers",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Area",
                table: "Groups",
                column: "Area")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ClusterId",
                table: "Groups",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_PlantingDate",
                table: "Groups",
                column: "PlantingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ReadyForUavDate",
                table: "Groups",
                column: "ReadyForUavDate");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_RiceVarietyId",
                table: "Groups",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SeasonId",
                table: "Groups",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Status",
                table: "Groups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SupervisorId",
                table: "Groups",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPrices_MaterialId",
                table: "MaterialPrices",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_IsActive",
                table: "Materials",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Manufacturer",
                table: "Materials",
                column: "Manufacturer");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Name",
                table: "Materials",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Type",
                table: "Materials",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_PlantingDate",
                table: "PlotCultivations",
                column: "PlantingDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_Plot_Season",
                table: "PlotCultivations",
                columns: new[] { "PlotId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_PlotId",
                table: "PlotCultivations",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_RiceVarietyId",
                table: "PlotCultivations",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_SeasonId",
                table: "PlotCultivations",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_Status",
                table: "PlotCultivations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PlotCultivation_Status_PlantingDate",
                table: "PlotCultivations",
                columns: new[] { "Status", "PlantingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Plots_Boundary",
                table: "Plots",
                column: "Boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_Coordinate",
                table: "Plots",
                column: "Coordinate")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_FarmerId",
                table: "Plots",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_FieldId",
                table: "Plots",
                column: "FieldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_GroupId",
                table: "Plots",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_SoilType",
                table: "Plots",
                column: "SoilType");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_Status",
                table: "Plots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_BasePlantingDate",
                table: "ProductionPlans",
                column: "BasePlantingDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_GroupId",
                table: "ProductionPlans",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_PlanName",
                table: "ProductionPlans",
                column: "PlanName");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_PlotCultivationId",
                table: "ProductionPlans",
                column: "PlotCultivationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_StandardPlanId",
                table: "ProductionPlans",
                column: "StandardPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_Status",
                table: "ProductionPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_Status_PlantingDate",
                table: "ProductionPlans",
                columns: new[] { "Status", "BasePlantingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_AgronomyExpertId",
                table: "ProductionPlans",
                column: "AgronomyExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_ApprovedBy",
                table: "ProductionPlans",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlans_SubmittedBy",
                table: "ProductionPlans",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTaskMaterial_MaterialId",
                table: "ProductionPlanTaskMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTaskMaterial_Task_Material",
                table: "ProductionPlanTaskMaterials",
                columns: new[] { "ProductionPlanTaskId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTaskMaterial_TaskId",
                table: "ProductionPlanTaskMaterials",
                column: "ProductionPlanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTaskMaterials_MaterialId1",
                table: "ProductionPlanTaskMaterials",
                column: "MaterialId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTask_Plan_Sequence",
                table: "ProductionPlanTasks",
                columns: new[] { "ProductionPlanId", "SequenceOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTask_ProductionPlanId",
                table: "ProductionPlanTasks",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTask_ScheduledDate",
                table: "ProductionPlanTasks",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTask_StandardPlanTaskId",
                table: "ProductionPlanTasks",
                column: "StandardPlanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlanTask_Status",
                table: "ProductionPlanTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVariety_BaseGrowthDuration",
                table: "RiceVarieties",
                column: "BaseGrowthDurationDays");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVariety_IsActive",
                table: "RiceVarieties",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVariety_VarietyName",
                table: "RiceVarieties",
                column: "VarietyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiceVarietySeason_IsRecommended",
                table: "RiceVarietySeasons",
                column: "IsRecommended");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVarietySeason_RiceVarietyId",
                table: "RiceVarietySeasons",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVarietySeason_RiskLevel",
                table: "RiceVarietySeasons",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVarietySeason_SeasonId",
                table: "RiceVarietySeasons",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_RiceVarietySeason_Variety_Season",
                table: "RiceVarietySeasons",
                columns: new[] { "RiceVarietyId", "SeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Season_DateRange",
                table: "Seasons",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Season_IsActive",
                table: "Seasons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Season_SeasonName",
                table: "Seasons",
                column: "SeasonName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Season_SeasonType",
                table: "Seasons",
                column: "SeasonType");

            migrationBuilder.CreateIndex(
                name: "IX_StandardPlans_CreatedBy",
                table: "StandardPlans",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StandardPlans_RiceVarietyId",
                table: "StandardPlans",
                column: "RiceVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardPlanTaskMaterials_MaterialId",
                table: "StandardPlanTaskMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardPlanTaskMaterials_StandardPlanTaskId",
                table: "StandardPlanTaskMaterials",
                column: "StandardPlanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardPlanTasks_StandardPlanId",
                table: "StandardPlanTasks",
                column: "StandardPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorFarmerAssignments_AssignedAt",
                table: "SupervisorFarmerAssignments",
                column: "AssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorFarmerAssignments_FarmerId",
                table: "SupervisorFarmerAssignments",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorFarmerAssignments_IsActive",
                table: "SupervisorFarmerAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorFarmerAssignments_SupervisorId",
                table: "SupervisorFarmerAssignments",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorFarmerAssignments_SupervisorId_FarmerId",
                table: "SupervisorFarmerAssignments",
                columns: new[] { "SupervisorId", "FarmerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Supervisors_CurrentFarmerCount",
                table: "Supervisors",
                column: "CurrentFarmerCount");

            migrationBuilder.CreateIndex(
                name: "IX_Supervisors_MaxFarmerCapacity",
                table: "Supervisors",
                column: "MaxFarmerCapacity");

            migrationBuilder.CreateIndex(
                name: "IX_UavInvoices_UavServiceOrderId",
                table: "UavInvoices",
                column: "UavServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UavInvoices_UavVendorId",
                table: "UavInvoices",
                column: "UavVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_UavServiceOrders_CreatedBy",
                table: "UavServiceOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UavServiceOrders_GroupId",
                table: "UavServiceOrders",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UavServiceOrders_UavVendorId",
                table: "UavServiceOrders",
                column: "UavVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_UavVendors_FleetSize",
                table: "UavVendors",
                column: "FleetSize");

            migrationBuilder.CreateIndex(
                name: "IX_UavVendors_ServiceRadius",
                table: "UavVendors",
                column: "ServiceRadius");

            migrationBuilder.CreateIndex(
                name: "IX_UavVendors_ServiceRatePerHa",
                table: "UavVendors",
                column: "ServiceRatePerHa");

            migrationBuilder.CreateIndex(
                name: "IX_UavVendors_VendorName",
                table: "UavVendors",
                column: "VendorName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ClusterManagers");

            migrationBuilder.DropTable(
                name: "CultivationTaskMaterials");

            migrationBuilder.DropTable(
                name: "FarmLogMaterials");

            migrationBuilder.DropTable(
                name: "FieldWeathers");

            migrationBuilder.DropTable(
                name: "MaterialPrices");

            migrationBuilder.DropTable(
                name: "ProductionPlanTaskMaterials");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RiceVarietySeasons");

            migrationBuilder.DropTable(
                name: "StandardPlanTaskMaterials");

            migrationBuilder.DropTable(
                name: "SupervisorFarmerAssignments");

            migrationBuilder.DropTable(
                name: "UavInvoices");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "FarmLogs");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "UavServiceOrders");

            migrationBuilder.DropTable(
                name: "CultivationTasks");

            migrationBuilder.DropTable(
                name: "ProductionPlanTasks");

            migrationBuilder.DropTable(
                name: "UavVendors");

            migrationBuilder.DropTable(
                name: "ProductionPlans");

            migrationBuilder.DropTable(
                name: "StandardPlanTasks");

            migrationBuilder.DropTable(
                name: "PlotCultivations");

            migrationBuilder.DropTable(
                name: "StandardPlans");

            migrationBuilder.DropTable(
                name: "Plots");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "AgronomyExperts");

            migrationBuilder.DropTable(
                name: "Farmers");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Clusters");

            migrationBuilder.DropTable(
                name: "RiceVarieties");

            migrationBuilder.DropTable(
                name: "Supervisors");

            migrationBuilder.DropTable(
                name: "ApplicationUser");
        }
    }
}
