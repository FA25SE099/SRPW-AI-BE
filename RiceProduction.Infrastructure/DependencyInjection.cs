using Ardalis.GuardClauses;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Domain.Entities;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Data.Interceptors;
using RiceProduction.Infrastructure.Identity;
using RiceProduction.Infrastructure.Implementation.MiniExcelImplementation;
using RiceProduction.Infrastructure.Implementation.NotificationImplementation.Infobip;
using RiceProduction.Infrastructure.Implementation.StorageImplementation.Azure;
using RiceProduction.Infrastructure.Implementation.Zalo;
using RiceProduction.Infrastructure.Repository;
using RiceProduction.Infrastructure.Services;
using System.Threading.Channels;

namespace RiceProduction.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("CleanArchitectureDb");
        Guard.Against.Null(connectionString, message: "Connection string 'CleanArchitectureDb' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, PlotGeometryInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
        });

        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ApplicationDbContextInitialiser>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddSingleton(_ =>
            Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false
            }));
        builder.Services.AddHostedService<QueuedEmailBackgroundService>();
        builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

        builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        builder.Services.AddScoped<IFarmerRepository, FarmerRepository>();
        builder.Services.AddScoped<ICacheInvalidator, CacheInvalidator>();

        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ITokenService, TokenService>();

        builder.Services.AddScoped<IGenericExcel, GenericExcel>();
        builder.Services.AddScoped<IFarmerExcel, FarmerExcelImplement>();

        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IEmailJobService, EmailJobService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddHttpClient<IZaloOAuthService, ZaloOAuthService>();
        builder.Services.AddHttpClient<IZaloZnsService, ZaloZnsService>();

        builder.Services.AddSingleton(_ =>
            new BlobServiceClient(builder.Configuration["AzureStorage:ConnectionString"]));
        builder.Services.AddScoped<IStorageService, AzureStorageService>();

        builder.Services.AddSingleton<ISmSService>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config["Infobip:ApiKey"] ?? string.Empty;
            var basePath = config["Infobip:BasePath"] ?? "https://api.infobip.com";
            var defaultSender = config["Infobip:DefaultSender"] ?? "InfoSMS";
            return new InfobipSmsAPI(apiKey, basePath, defaultSender);
        });

        builder.Services.AddScoped<IRicePestDetectionService, RicePestDetectionService>();
        builder.Services.AddScoped<IContextualAIService, ContextualGeminiAIService>();
        builder.Services.AddScoped<IPostGISGroupFormationService, Services.PostGISGroupFormationService>();
    }
}
