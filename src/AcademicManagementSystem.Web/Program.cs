using AcademicManagementSystem.Web.Data;
using AcademicManagementSystem.Web.Models;
using AcademicManagementSystem.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

// Version 2 intentionally uses fresh databases. The old demonstration
// database remains untouched so an update never silently destroys work.
var academicDatabasePath = Path.Combine(dataDirectory, "academic-management-v2.db");
var identityDatabasePath = Path.Combine(dataDirectory, "academic-identity-v2.db");

builder.Services.AddDbContext<AcademicManagementDbContext>(options =>
    options.UseSqlite($"Data Source={academicDatabasePath}"));
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
    options.UseSqlite($"Data Source={identityDatabasePath}"));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<AcademicSettingsService>();
builder.Services.AddScoped<CurrentStudentService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<AcademicProgressionService>();
builder.Services.AddScoped<StudentAccessService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<AcademicRiskService>();
builder.Services.AddScoped<TimetableService>();

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
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var academicDatabase = services.GetRequiredService<AcademicManagementDbContext>();
    var identityDatabase = services.GetRequiredService<ApplicationIdentityDbContext>();
    await DbInitializer.InitialiseAsync(academicDatabase);
    await academicDatabase.Database.MigrateAsync();
    await identityDatabase.Database.EnsureCreatedAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentityInitializer.EnsureRolesAsync(roleManager);
}

app.Run();
