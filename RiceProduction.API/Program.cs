using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using RiceProduction.API.Common;
using RiceProduction.API.Middlewares;
using RiceProduction.API.Services;
using RiceProduction.Application;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Application.Common.Interfaces.External;
using RiceProduction.Infrastructure;
using RiceProduction.Infrastructure.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(
        uri: "http://loki:3100",
        labels: new[]
        {
            new LokiLabel { Key = "application", Value = "rice-production-api" },
            new LokiLabel { Key = "environment", Value = "production" }
        },
        propertiesAsLabels: new[] { "level" },
        textFormatter: new LokiJsonTextFormatter()
    )
    .CreateBootstrapLogger();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

var isProduction = builder.Configuration.GetValue<bool>("IsProduction");

builder.AddApplicationServices();
builder.AddInfrastructureServices();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rice Production API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. " +
                      "Enter *only the token* (without 'Bearer ' prefix) in the text input below. " +
                      "Example: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "https://srpw-ai-fe-phtr.vercel.app",
                "https://srpw-ai-fe-i3gi-dxwvy4apx-trincse182497-1426s-projects.vercel.app/",
                "http://riceproduction.online",
                "https://riceproduction.online",
                "http://www.riceproduction.online",
                "https://www.riceproduction.online")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Location");
    });

    options.AddPolicy("AllowGemini", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition");
    });

    options.AddPolicy("AllowRiceProduction", policy =>
    {
        policy.WithOrigins(
                "http://riceproduction.online",
                "https://riceproduction.online",
                "http://www.riceproduction.online",
                "https://www.riceproduction.online")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUser, CurrentUser>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT Secret not configured");
}

var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();
var seedDatabase = builder.Configuration.GetValue<bool>("SeedDatabase");

if (seedDatabase)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var initializer = services.GetRequiredService<ApplicationDbContextInitialiser>();

        if (app.Environment.IsDevelopment() && !isProduction)
        {
            await initializer.InitialiseAsync();
        }

        if (isProduction)
        {
            await initializer.ResetDatabaseAsync();
        }

        await initializer.SeedAsyncAdminOnly();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB or seeding data.");
    }
}

app.MapPost("/api/rice/check-pest", async (
    [FromForm] IFormFileCollection files,
    [FromServices] IRicePestDetectionService detectionService) =>
{
    if (files == null || files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files uploaded" });
    }

    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
    const long maxFileSizeBytes = 10 * 1024 * 1024;

    foreach (var file in files)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return Results.BadRequest(new { error = $"File '{file.FileName}' has invalid extension. Only JPG, JPEG, and PNG files are allowed" });
        }

        if (file.Length > maxFileSizeBytes)
        {
            return Results.BadRequest(new { error = $"File '{file.FileName}' size must not exceed 10MB" });
        }
    }

    try
    {
        var results = new List<object>();
        foreach (var file in files)
        {
            var result = await detectionService.DetectPestAsync(file);
            results.Add(result);
        }

        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error processing image"
        );
    }
})
.DisableAntiforgery()
.WithName("CheckRicePest")
.WithOpenApi();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseCors("AllowGemini");
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseHttpsRedirection();
app.UseMiddleware<LoggingMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
