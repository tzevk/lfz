using System.Text.Json;
using LFZ.Domain.Entities;
using LFZ.Domain.Enums;
using LFZ.Infrastructure.Data;
using LFZ.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace LFZ.Infrastructure.Seed;

public static class SeedData
{
    public static readonly string[] Roles = { "Viewer", "Requester", "Allocator", "Admin" };

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

        // 1) Seed roles
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2) Seed one pilot account for each role
        foreach (var role in Roles)
        {
            var email = $"{role.ToLowerInvariant()}@example.com";
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = $"Pilot {role}",
                    RoleFlag = role,
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(user, $"{role}123");
            }
            else if (user.RoleFlag != role)
            {
                user.RoleFlag = role;
                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        await SeedAppSettingsAsync(context);
        await SeedPlotsAsync(context, environment);
    }

    private static async Task SeedAppSettingsAsync(ApplicationDbContext context)
    {
        var defaults = new[]
        {
            new AppSetting { Key = "Feature.AllowMultiTenantBlock", Value = "false", ValueType = "Boolean", Description = "Global gate for the multi-tenant plot block exception." },
            new AppSetting { Key = "Palette.Plot.Free", Value = "#5BBF72", ValueType = "Color", Description = "Map colour for free plots." },
            new AppSetting { Key = "Palette.Plot.Blocked", Value = "#F0B84D", ValueType = "Color", Description = "Map colour for blocked plots." },
            new AppSetting { Key = "Palette.Plot.Occupied", Value = "#3B82C4", ValueType = "Color", Description = "Map colour for occupied plots." },
            new AppSetting { Key = "Palette.Plot.PendingReview", Value = "#9B7BD1", ValueType = "Color", Description = "Map colour for plots pending review." },
            new AppSetting { Key = "Palette.Plot.Unavailable", Value = "#9CA3AF", ValueType = "Color", Description = "Map colour for unavailable plots." }
        };

        foreach (var setting in defaults)
        {
            if (!await context.AppSettings.AnyAsync(item => item.Key == setting.Key))
            {
                context.AppSettings.Add(setting);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPlotsAsync(ApplicationDbContext context, IHostEnvironment environment)
    {
        if (await context.Plots.AnyAsync())
        {
            return;
        }

        var seedPath = ResolvePlotSeedPath(environment);
        if (!File.Exists(seedPath))
        {
            return;
        }

        var seedJson = await File.ReadAllTextAsync(seedPath);
        var seedItems = JsonSerializer.Deserialize<List<PlotSeedItem>>(seedJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<PlotSeedItem>();

        var wktReader = new WKTReader();

        foreach (var item in seedItems.Where(item => !string.IsNullOrWhiteSpace(item.Code)))
        {
            Geometry? boundary = null;
            if (!string.IsNullOrWhiteSpace(item.BoundaryWkt))
            {
                boundary = wktReader.Read(item.BoundaryWkt);
                boundary.SRID = 0;
            }

            context.Plots.Add(new Plot
            {
                Code = item.Code.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.Code.Trim() : item.DisplayName.Trim(),
                LandUseType = string.IsNullOrWhiteSpace(item.LandUseType) ? "Unspecified" : item.LandUseType.Trim(),
                Phase = item.Phase,
                HatchColor = item.HatchColor,
                AreaHectares = item.AreaHectares,
                Status = ParsePlotStatus(item.Status),
                SvgPath = item.SvgPath,
                Boundary = boundary,
                Centroid = item.Centroid is null ? null : new Point(item.Centroid.X, item.Centroid.Y),
                IsLocked = item.IsLocked,
                MultiTenantBlockEnabled = item.MultiTenantBlockEnabled
            });
        }

        await context.SaveChangesAsync();
    }

    private static PlotStatus ParsePlotStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "free" => PlotStatus.Free,
            "available" => PlotStatus.Free,
            "occupied" => PlotStatus.Occupied,
            "allocated" => PlotStatus.Occupied,
            "pending review" => PlotStatus.PendingReview,
            "under review" => PlotStatus.PendingReview,
            _ when Enum.TryParse<PlotStatus>(status, true, out var parsedStatus) => parsedStatus,
            _ => PlotStatus.Free
        };
    }

    private static string ResolvePlotSeedPath(IHostEnvironment environment)
    {
        var contentRootSeed = Path.Combine(environment.ContentRootPath, "Seed", "plots-seed.json");
        if (File.Exists(contentRootSeed))
        {
            return contentRootSeed;
        }

        return Path.Combine(AppContext.BaseDirectory, "Seed", "plots-seed.json");
    }

    private sealed class PlotSeedItem
    {
        public string Code { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? LandUseType { get; set; }
        public string? Phase { get; set; }
        public string? HatchColor { get; set; }
        public decimal AreaHectares { get; set; }
        public string? Status { get; set; }
        public string? SvgPath { get; set; }
        public PlotSeedCentroid? Centroid { get; set; }
        public bool IsLocked { get; set; }
        public bool MultiTenantBlockEnabled { get; set; }
        public string? BoundaryWkt { get; set; }
    }

    private sealed class PlotSeedCentroid
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
