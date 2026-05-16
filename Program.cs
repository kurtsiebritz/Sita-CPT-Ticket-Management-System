using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SitaCptTicketApp.Data;
using SitaCptTicketApp.Models;
using SitaCptTicketApp.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.Extensions.Options;
using SitaCptTicketApp.Controllers;

var builder = WebApplication.CreateBuilder(args);

// ====================== DbContext (LocalDB) ======================
builder.Services.AddDbContext<SitaCptTicketAppContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("SitaCptTicketAppContextConnection");
    options.UseSqlServer(conn, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));
});

// ====================== Data Protection ======================
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "Keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("SitaCptTicketApp");

// ====================== Identity ======================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<SitaCptTicketAppContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// ====================== Authentication ======================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SitaCptTicketApp.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.LoginPath = "/Identity/Account/Login";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";

        options.Events.OnSignedIn = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userName = context.Principal.Identity?.Name ?? "Anonymous";
            logger.LogInformation("User {UserName} signed in", userName);
            return Task.CompletedTask;
        };
    });

// ====================== Authorization ======================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireAssertion(ctx =>
        {
            var isAuth = ctx.User.Identity?.IsAuthenticated ?? false;
            var isAdmin = ctx.User.HasClaim(c => c.Type == "IsAdmin" &&
                                               c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
            return isAuth && isAdmin;
        }));
});

// ====================== Services ======================
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomClaimsPrincipalFactory>();
builder.Services.AddScoped<IncidentParserService>();
builder.Services.AddScoped<LogAuthorizationFilter>();
builder.Services.AddScoped<GlobalRequestLoggingFilter>();

// ====================== MVC + Razor Pages ======================
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.AddService<GlobalRequestLoggingFilter>();
});

builder.Services.AddRazorPages();

var app = builder.Build();

// ====================== Pipeline ======================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Root redirect to login
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" && !context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Identity/Account/Login");
        return;
    }
    await next();
});

// ====================== Routes ======================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ====================== Migrate + Seed Sample Data ======================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<SitaCptTicketAppContext>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Database migration completed successfully.");

        // Seed realistic sample data for GitHub portfolio
        logger.LogInformation("🌱 Seeding sample data...");
        await SeedData.Initialize(services);
        logger.LogInformation("✅ Sample data seeded successfully!");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "❌ Error during migration or seeding!");
        throw;
    }
}

app.Run();

// ====================== Custom Classes ======================
public class CustomClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly ILogger<CustomClaimsPrincipalFactory> _logger;

    public CustomClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<CustomClaimsPrincipalFactory> logger)
        : base(userManager, roleManager, optionsAccessor)
    {
        _logger = logger;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        principal.Identities.First().AddClaim(new Claim("IsAdmin", user.IsAdmin.ToString()));
        _logger.LogInformation("Added IsAdmin claim for {Email}: {IsAdmin}", user.Email, user.IsAdmin);
        return principal;
    }
}

public class GlobalRequestLoggingFilter : IActionFilter
{
    private readonly ILogger<GlobalRequestLoggingFilter> _logger;

    public GlobalRequestLoggingFilter(ILogger<GlobalRequestLoggingFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User.Identity?.Name ?? "Anonymous";
        var path = context.HttpContext.Request.Path;
        _logger.LogInformation("Request → {Path} by {User}", path, user);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}