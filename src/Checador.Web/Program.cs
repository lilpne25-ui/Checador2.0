using Checador.Web.FeatureFlags;
using Checador.Web.Security;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "CHECADOR_");

var moduleFlags = builder.Configuration.GetSection("FeatureFlags:Modules").Get<WebModuleFeatureFlags>() ?? new WebModuleFeatureFlags();
var demoMode = string.Equals(
    Environment.GetEnvironmentVariable("CHECADOR_DEMO_MODE"),
    "true",
    StringComparison.OrdinalIgnoreCase);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = demoMode ? "Checador.Web.Demo.Auth" : "Checador.Web.Auth";
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("RestrictedModulesOnly", policy =>
    {
        policy.RequireAssertion(context => SupervisorModuleAccess.CanAccessRestrictedModules(context.User));
    });
});

builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Login");
    });

builder.Services.AddHttpClient("ChecadorApi");
builder.Services.AddScoped<ChecadorApiClient>();
builder.Services.AddSingleton(moduleFlags);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (TryGetDisabledWebModule(context.Request.Path, moduleFlags, out var moduleName))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync($"Módulo '{moduleName}' deshabilitado temporalmente por feature flag.", context.RequestAborted);
        return;
    }

    await next();
});

app.MapRazorPages();
app.MapGet("/logout", async context =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/Login");
}).RequireAuthorization();

app.Run();

static bool TryGetDisabledWebModule(PathString path, WebModuleFeatureFlags flags, out string moduleName)
{
    moduleName = string.Empty;
    var value = path.Value ?? string.Empty;

    if (value.StartsWith("/Ops", StringComparison.OrdinalIgnoreCase) && !flags.EnableOps)
    {
        moduleName = "ops";
        return true;
    }

    if ((value.StartsWith("/Admin/Employees", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/Admin/EmployeeEdit", StringComparison.OrdinalIgnoreCase))
        && !flags.EnableEmployees)
    {
        moduleName = "empleados";
        return true;
    }

    if (value.StartsWith("/Admin/Reassign", StringComparison.OrdinalIgnoreCase) && !flags.EnableReassign)
    {
        moduleName = "cambio-por-rango";
        return true;
    }

    if (value.StartsWith("/Admin/Pervasive", StringComparison.OrdinalIgnoreCase) && !flags.EnablePervasive)
    {
        moduleName = "pervasive";
        return true;
    }

    if (value.StartsWith("/Admin/ManualPunch", StringComparison.OrdinalIgnoreCase) && !flags.EnableManualPunch)
    {
        moduleName = "alta-manual";
        return true;
    }

    if (value.StartsWith("/Admin/Schedules", StringComparison.OrdinalIgnoreCase) && !flags.EnableSchedules)
    {
        moduleName = "horarios";
        return true;
    }

    if (value.StartsWith("/Admin/Reports", StringComparison.OrdinalIgnoreCase) && !flags.EnableReports)
    {
        moduleName = "reportes";
        return true;
    }

    if (value.StartsWith("/Admin/Users", StringComparison.OrdinalIgnoreCase) && !flags.EnableAdminUsers)
    {
        moduleName = "admin-users";
        return true;
    }

    return false;
}
