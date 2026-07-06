using LFZ.Application.Abstractions;
using LFZ.Application.Services;
using LFZ.Infrastructure.Data;
using LFZ.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LFZ.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database, Identity core, audit interceptor and application services.
    /// Authentication schemes (JWT for the API, cookies for the Web app) are configured by each host.
    /// </summary>
    public static IServiceCollection AddLfzInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddScoped<PlotStatusAuditContext>();
        services.AddScoped<PlotStatusHistoryInterceptor>();
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseSqlServer(connectionString, sqlOptions => sqlOptions.UseNetTopologySuite())
                .AddInterceptors(serviceProvider.GetRequiredService<PlotStatusHistoryInterceptor>()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<PlotCommandService>();
        services.AddScoped<PlotQueryService>();

        return services;
    }

    public static IdentityOptions ConfigureLfzIdentityOptions(this IdentityOptions options)
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        return options;
    }

    /// <summary>Named role policies shared by the API (JWT) and the Web app (cookies).</summary>
    public static AuthorizationOptions AddLfzPolicies(this AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy("ViewerOnly", policy => policy.RequireRole("Viewer"));
        options.AddPolicy("RequesterOnly", policy => policy.RequireRole("Requester"));
        options.AddPolicy("AllocatorOnly", policy => policy.RequireRole("Allocator"));
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

        options.AddPolicy("CanViewDashboard", policy =>
            policy.RequireRole("Viewer", "Requester", "Allocator", "Admin"));

        options.AddPolicy("CanRequestPlot", policy =>
            policy.RequireRole("Requester", "Allocator", "Admin"));

        options.AddPolicy("CanAllocatePlot", policy =>
            policy.RequireRole("Allocator", "Admin"));

        options.AddPolicy("CanBlockPlot", policy =>
            policy.RequireRole("Allocator", "Admin"));

        options.AddPolicy("CanManageSettings", policy =>
            policy.RequireRole("Admin"));

        return options;
    }
}
