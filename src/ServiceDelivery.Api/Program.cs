using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceDelivery.Api.Hubs;
using ServiceDelivery.Api.Services;
using ServiceDelivery.Application.Common.Interfaces;
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

// Application service registrations
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

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
