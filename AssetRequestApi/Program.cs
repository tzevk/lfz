using System.Text;
using AssetRequestApi.Data;
using AssetRequestApi.Hubs;
using AssetRequestApi.Models;
using AssetRequestApi.Seed;
using AssetRequestApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1) Database (SQL Server)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddScoped<PlotStatusAuditContext>();
builder.Services.AddScoped<PlotStatusHistoryInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.UseNetTopologySuite())
        .AddInterceptors(serviceProvider.GetRequiredService<PlotStatusHistoryInterceptor>()));

// ---------------------------------------------------------------------------
// 2) Identity
// ---------------------------------------------------------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
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
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// 3) JWT Bearer Authentication
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ---------------------------------------------------------------------------
// 4) Named authorization policies
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
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

    options.AddPolicy("CanViewAll", policy =>
        policy.RequireRole("Viewer", "Requester", "Allocator", "Admin"));

    options.AddPolicy("CanCreateRequest", policy =>
        policy.RequireRole("Requester", "Allocator", "Admin"));

    options.AddPolicy("CanAllocate", policy =>
        policy.RequireRole("Allocator", "Admin"));
});

// ---------------------------------------------------------------------------
// 5) Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<PlotCommandService>();
builder.Services.AddSignalR();
builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// 6) Swagger with JWT Bearer support
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Asset Request Management API",
        Version = "v1",
        Description = "ASP.NET Core Identity + JWT + Role-based authorization demo"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here (no need to type 'Bearer ' prefix)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

var app = builder.Build();

// ---------------------------------------------------------------------------
// 7) Seed roles + default admin at startup
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

// ---------------------------------------------------------------------------
// 8) Middleware pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Asset Request Management API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PlotHub>("/hubs/plots").RequireAuthorization();

app.Run();
