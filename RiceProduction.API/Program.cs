using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RiceProduction.API.Middlewares;
using RiceProduction.API.Services;
using RiceProduction.Application;
using RiceProduction.Application.Common.Interfaces;
using RiceProduction.Infrastructure;
using RiceProduction.Infrastructure.Data;
using RiceProduction.Infrastructure.Implementation.MiniExcelImplementation;
using System.Text;
using System.Text.Json.Serialization;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    Log.Information("Starting Rice Production API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();
var otel = builder.Configuration.GetSection("OpenTelemetry");
var serviceName = otel["ServiceName"] ?? "RiceProduction.API";
var serviceVersion = otel["ServiceVersion"] ?? "1.0.0";
var otlpEndpoint = otel.GetSection("Otlp")["Endpoint"];
var otlpHeaders = otel.GetSection("Otlp")["Headers"];
var isProduction = builder.Configuration.GetValue<bool>("IsProduction"); // or builder.Environment.IsProduction()

//if (!string.IsNullOrEmpty(otlpEndpoint))
//{
//    var resourceBuilder = ResourceBuilder.CreateDefault()
//        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
//        .AddAttributes(new Dictionary<string, object>
//        {
//            ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
//            ["host.name"] = Environment.MachineName
//            // add more static attributes if needed
//        });

//    // === Logs ===
//    builder.Logging.AddOpenTelemetry(logging =>
//    {
//        logging.SetResourceBuilder(resourceBuilder);
//        logging.IncludeFormattedMessage = true;
//        logging.IncludeScopes = true;
//        logging.ParseStateValues = true;

//        logging.AddOtlpExporter(otlp =>
//        {
//            otlp.Endpoint = new Uri(otlpEndpoint!); // ! asserts non-null because we checked above
//            otlp.Headers = otlpHeaders;             // e.g. "Authorization=Basic ..."
//        });
//    });

//    // === Traces + Metrics ===
//    builder.Services.AddOpenTelemetry()
//        .ConfigureResource(rb => rb.Clear().AddService(serviceName, serviceVersion)) // or reuse resourceBuilder
//        .WithTracing(tracing => tracing
//            .SetResourceBuilder(resourceBuilder)
//            .SetErrorStatusOnException()               // recommended
//            .AddAspNetCoreInstrumentation()
//            .AddHttpClientInstrumentation()
//            .AddOtlpExporter(otlp =>
//            {
//                otlp.Endpoint = new Uri(otlpEndpoint);
//                otlp.Headers = otlpHeaders;
//            }))
//        .WithMetrics(metrics => metrics
//            .SetResourceBuilder(resourceBuilder)
//            .AddAspNetCoreInstrumentation()
//            .AddHttpClientInstrumentation()
//            .AddRuntimeInstrumentation()
//            .AddOtlpExporter(otlp =>
//            {
//                otlp.Endpoint = new Uri(otlpEndpoint);
//                otlp.Headers = otlpHeaders;
//            }));
//}


builder.AddApplicationServices();
builder.AddInfrastructureServices();

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
                      "Enter **only the token** (without 'Bearer ' prefix) in the text input below. " +
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
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") 
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
        var context = services.GetRequiredService<ApplicationDbContext>();
        var initializer = services.GetRequiredService<ApplicationDbContextInitialiser>();

        if (app.Environment.IsDevelopment() && !isProduction)
        {
            await initializer.InitialiseAsync();
        }
        //if (isProduction)
        //{
        //    await initializer.ResetDatabaseAsync();
        //}
        if (isProduction)
        {
            await context.Database.MigrateAsync();
        }
        await initializer.SeedAsyncAdminOnly();
        //await initializer.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB or seeding data.");
    }
}
    app.UseSwagger();
    app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseCors("AllowGemini");

app.UseHttpsRedirection();
app.UseMiddleware<LoggingMiddleware>();
// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}