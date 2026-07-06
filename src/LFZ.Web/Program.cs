using LFZ.Infrastructure;
using LFZ.Infrastructure.Data;
using LFZ.Infrastructure.Identity;
using LFZ.Infrastructure.Seed;
using LFZ.Web.Components;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure: SQL Server + NTS, audit interceptor, application services
builder.Services.AddLfzInfrastructure(builder.Configuration);

// Identity with cookie authentication (interactive web app)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.ConfigureLfzIdentityOptions())
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options => options.AddLfzPolicies());
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Seed roles, pilot users, settings and plots at startup
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ---------------------------------------------------------------------------
// Cookie sign-in endpoints (outside the Blazor circuit)
// ---------------------------------------------------------------------------
app.MapPost("/account/login", async (
    HttpContext http,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();

    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        return Results.Redirect("/login?failed=1");
    }

    var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
    return result.Succeeded
        ? Results.Redirect("/")
        : Results.Redirect(result.IsLockedOut ? "/login?locked=1" : "/login?failed=1");
}).AllowAnonymous();

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
