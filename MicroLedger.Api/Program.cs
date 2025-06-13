using MicroLedger.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;
using MicroLedger.Api.Models;
using MicroLedger.Api.Controllers;
using MicroLedger.Domain;
using MicroLedger.Application;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using MicroLedger.Application.Services;
using MicroLedger.Application.Services.Interfaces;
using MicroLedger.Infrastructure.Services;
using MassTransit;
using MicroLedger.Domain.Services;
using MicroLedger.Domain.Interfaces;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure culture settings
var defaultCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
CultureInfo.CurrentCulture = defaultCulture;
CultureInfo.CurrentUICulture = defaultCulture;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "MicroLedger.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("MicroLedger.Api")
        .AddConsoleExporter());

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MicroLedger API",
        Version = "v1",
        Description = "A double-entry ledger microservice API"
    });

    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
builder.Services.AddSingleton(jwtSettings);

// Configure JWT Bearer authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(token) && !token.StartsWith("Bearer "))
                {
                    context.Request.Headers["Authorization"] = $"Bearer {token}";
                }
                return Task.CompletedTask;
            }
        };
    });

// Configure DbContext
builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LedgerDb")));

// Register DbContext interface
builder.Services.AddScoped<ILedgerDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LedgerDbContext>();

builder.Services.AddScoped<IPaymentService, PaymentService>();
//builder.Services.AddScoped<MicroLedger.Domain.Services.IInterestService, MicroLedger.Infrastructure.Services.InterestService>();
builder.Services.AddScoped<MicroLedger.Domain.Services.IBalanceService, MicroLedger.Application.Services.BalanceService>();
builder.Services.AddHostedService<MicroLedger.Infrastructure.InterestService>();

// Add MassTransit configuration
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Configure message serialization
        cfg.UseJsonSerializer();
        cfg.UseJsonDeserializer();

        // Configure endpoints
        cfg.ConfigureEndpoints(context);
    });
});

// Register outbox services
builder.Services.AddScoped<IOutboxService, OutboxService>();

builder.Services.AddGrpc();

var app = builder.Build();

// Seed test data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    await MicroLedger.Infrastructure.TestDataSeeder.SeedTestDataAsync(dbContext);
}

// Configure the HTTP request pipeline
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c =>
//    {
//        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MicroLedger API V1");
//        c.RoutePrefix = "swagger";
//    });
//}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MicroLedger API V1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

// Map gRPC service
app.MapGrpcService<MicroLedger.Infrastructure.Services.BalanceGrpcService>();

app.Run();

// Make Program class public and partial
public partial class Program
{
    // JwtSettings class moved here for clarity
    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }
}