using AquaTrack.Application;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Infrastructure;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence.Seed;
using AquaTrack.Web.Controllers;
using AquaTrack.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF's community licence covers this project; it must be set before any PDF is produced.
QuestPDF.Settings.License = LicenseType.Community;

// Composition root: the layers are wired here and nowhere else.
builder.Services.AddAquaTrackApplication();
builder.Services.AddAquaTrackInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IFeedAdvisorAccessor, FeedAdvisorAccessor>();

builder.Services.AddControllersWithViews();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.Name = "AquaTrack.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // HTMX would swap a 302's HTML into the page, so send it a redirect instruction instead.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.IsHtmx())
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers[Htmx.RedirectHeader] = context.RedirectUri;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.IsHtmx())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers[Htmx.RedirectHeader] = context.RedirectUri;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ManageFarm, policy => policy.RequireRole(Roles.Admin, Roles.Manager));
    options.AddPolicy(Policies.RecordDailyData, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Worker));
    options.AddPolicy(Policies.Administer, policy => policy.RequireRole(Roles.Admin));
});

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    // Lets any controller resolve a shared partial by name alone.
    options.ViewLocationFormats.Add("/Views/Shared/Partials/{0}.cshtml");
});

builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");

    // A 404 or 403 re-executes onto the status page, so the user keeps the app chrome
    // instead of landing on a blank browser error.
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

await DbSeeder.SeedAsync(app.Services);

app.Run();

/// <summary>Named authorization policies, so the role lists live in exactly one place.</summary>
public static class Policies
{
    public const string ManageFarm = "ManageFarm";
    public const string RecordDailyData = "RecordDailyData";
    public const string Administer = "Administer";
}
