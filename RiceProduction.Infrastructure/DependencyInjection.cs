using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Data.Interceptors;
using RiceProduction.Infrastructure.Identity;
using AutoMapper;
using RiceProduction.Application.Common.Mappings;
using RiceProduction.Application.PlotFeature.Queries;
using RiceProduction.Application.FarmerFeature.Queries;
using RiceProduction.Infrastructure.Implementation.MiniExcelImplementation;
using RiceProduction.Infrastructure.Implementation.NotificationImplementation.SpeedSMS;
using RiceProduction.Infrastructure.Implementation.Zalo;

namespace RiceProduction.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("CleanArchitectureDb");
        Guard.Against.Null(connectionString, message: "Connection string 'CleanArchitectureDb' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<IDownloadGenericExcel, DownloadGenericExcel>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();

                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });


        builder.Services.AddScoped<ApplicationDbContextInitialiser>();
        builder.Services.AddAutoMapper(typeof(FarmerMapping).Assembly);

        // Configure Identity with proper stores
        builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password settings
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;

            // Lockout settings
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
       
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GetAllFarmerQueriesHandler).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(GetAllPlotQueriesHandler).Assembly);
        });

        builder.Services.AddScoped<IFarmerExcel, FarmerExcelImplement>();
        builder.Services.AddScoped<ApplicationDbContextInitialiser>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        builder.Services.AddScoped<ISmSService, SpeedSMSAPI>();


        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ITokenService, TokenService>();

        // Register Zalo Services
        builder.Services.AddHttpClient<IZaloOAuthService, ZaloOAuthService>();
        builder.Services.AddHttpClient<IZaloZnsService, ZaloZnsService>();

    }
}