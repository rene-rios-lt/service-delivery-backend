using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceDelivery.Api.BackgroundServices;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Api.Services;
using ServiceDelivery.Application.Common.Interfaces;
using ServiceDelivery.Application.Common.Services;
using ServiceDelivery.Application.Features.Auth.Commands;
using ServiceDelivery.Domain.Interfaces;
using ServiceDelivery.Infrastructure.Persistence;
using ServiceDelivery.Infrastructure.Persistence.Seed;
using ServiceDelivery.Infrastructure.Repositories;
using ServiceDelivery.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("ServiceDeliveryDb"));

builder.Services.AddScoped<DataSeeder>();

// Repository registrations
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IRepSessionRepository, RepSessionRepository>();
builder.Services.AddScoped<IRepStateRepository, RepStateRepository>();
builder.Services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
builder.Services.AddScoped<IDiagnosticTroubleCodeRepository, DiagnosticTroubleCodeRepository>();
builder.Services.AddScoped<IJobOfferRepository, JobOfferRepository>();

// Application service registrations
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IExpiredJobOfferSweeper, ExpiredJobOfferSweeper>();
builder.Services.AddScoped<IPendingRequestReconciler, PendingRequestReconciler>();
builder.Services.AddScoped<IStaleHeartbeatSweeper, StaleHeartbeatSweeper>();

// Hub service registrations
builder.Services.AddScoped<IVehiclePositionHubService, VehiclePositionHubService>();
builder.Services.AddScoped<IDispatchHubService, DispatchHubService>();
builder.Services.AddScoped<IRepHubService, RepHubService>();
builder.Services.AddScoped<IRequesterHubService, RequesterHubService>();

// MediatR — scans the Application assembly for all handlers
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly));

// JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// Job offer expiry background sweep (first hosted service in the codebase — see plan D1)
builder.Services.Configure<JobOfferExpiryOptions>(builder.Configuration.GetSection("JobOfferExpiry"));
builder.Services.AddHostedService<ExpireJobOffersBackgroundService>();

// Orphaned pending-request reconciler — periodic backstop that re-matches Pending requests left
// with no Pending offer (mirrors the BE-018 hosted-service pattern; see plan BE-029).
builder.Services.Configure<ReconcilePendingRequestsOptions>(builder.Configuration.GetSection("ReconcilePendingRequests"));
builder.Services.AddHostedService<ReconcilePendingRequestsBackgroundService>();

// Heartbeat timeout background sweep (BE-028). The timer cadence (HeartbeatTimeoutOptions, Api) and the
// staleness threshold (HeartbeatTimeoutSettings, Application) bind from the same "HeartbeatTimeout" section.
// HeartbeatTimeoutSettings is a plain Application-layer class (Application has no IOptions dependency), so
// it is bound here and registered as a singleton instance for direct injection into the sweeper.
builder.Services.Configure<HeartbeatTimeoutOptions>(builder.Configuration.GetSection("HeartbeatTimeout"));
var heartbeatTimeoutSettings = builder.Configuration.GetSection("HeartbeatTimeout").Get<HeartbeatTimeoutSettings>()
    ?? new HeartbeatTimeoutSettings();
builder.Services.AddSingleton(heartbeatTimeoutSettings);
builder.Services.AddHostedService<HeartbeatTimeoutBackgroundService>();

// Redirect cooldown window (BE-022). RedirectOptions is a plain Application-layer class (Application has
// no IOptions dependency), bound from the "Redirect" section and registered as a singleton for direct
// injection into RedirectRepCommandHandler — mirrors the HeartbeatTimeoutSettings pattern above.
var redirectOptions = builder.Configuration.GetSection("Redirect").Get<RedirectOptions>()
    ?? new RedirectOptions();
builder.Services.AddSingleton(redirectOptions);

// JWT Bearer authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<VehiclePositionHub>("/hubs/position");
app.MapHub<DispatchHub>("/hubs/dispatch");
app.MapHub<RepHub>("/hubs/rep");
app.MapHub<RequesterHub>("/hubs/requester");

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

app.Run();

public partial class Program { }
