using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Middleware;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ── Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Cookie auth settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

builder.Services.AddControllersWithViews();

// ── In-memory cache for dashboard aggregations (5-min TTL)
builder.Services.AddMemoryCache();

// ── Executive Decision Support Services
builder.Services.AddScoped<IKPICalculationService, KpiCalculationService>();
builder.Services.AddScoped<IForecastingService, ForecastingService>();
builder.Services.AddScoped<IScenarioService, ScenarioService>();
builder.Services.AddScoped<IRiskAnalysisService, RiskAnalysisService>();

// ── PayMongo — configure the named client at registration time
builder.Services.AddHttpClient("PayMongo", (sp, client) =>
{
    var secretKey = Environment.GetEnvironmentVariable("PAYMONGO_SECRET_KEY")
        ?? sp.GetRequiredService<IConfiguration>()["PayMongo:SecretKey"]
        ?? "";

    var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secretKey + ":"));
    client.BaseAddress = new Uri("https://api.paymongo.com/v1/");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encoded);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<IPayMongoService, PayMongoService>();

// ── Security: singleton rate limiter (shared across all requests)
builder.Services.AddSingleton<LoginRateLimiter>();

// ── Email notification service
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    if (!app.Environment.IsDevelopment())
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
        if (connStr.Contains("Password=;") || connStr.Contains("Password=\"\";"))
            logger.LogWarning("⚠️  DATABASE PASSWORD IS EMPTY.");
    }

    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `LoginAttempts` (
                `Id`          INT NOT NULL AUTO_INCREMENT,
                `UserName`    VARCHAR(256) NOT NULL DEFAULT '',
                `IpAddress`   VARCHAR(45)  NOT NULL DEFAULT '',
                `UserAgent`   VARCHAR(250) NOT NULL DEFAULT '',
                `Succeeded`   TINYINT(1)   NOT NULL DEFAULT 0,
                `FailReason`  VARCHAR(100) NOT NULL DEFAULT '',
                `AttemptedAt` DATETIME(6)  NOT NULL DEFAULT '0001-01-01 00:00:00.000000',
                PRIMARY KEY (`Id`),
                INDEX `IX_LoginAttempts_AttemptedAt` (`AttemptedAt`),
                INDEX `IX_LoginAttempts_IpAddress_AttemptedAt` (`IpAddress`, `AttemptedAt`)
            ) CHARACTER SET utf8mb4;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE `PaymentTransactions`
            ADD COLUMN IF NOT EXISTS `PendingSaleDataJson` LONGTEXT NULL;
        ");

        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `SpoilageRecords` (
                `Id`            INT NOT NULL AUTO_INCREMENT,
                `ProductId`     INT NOT NULL,
                `Quantity`      INT NOT NULL DEFAULT 0,
                `EstimatedLoss` DECIMAL(18,2) NOT NULL DEFAULT 0,
                `Reason`        VARCHAR(100) NOT NULL DEFAULT 'Overripe',
                `RecordedBy`    VARCHAR(256) NOT NULL DEFAULT '',
                `RecordedAt`    DATETIME(6)  NOT NULL DEFAULT '0001-01-01 00:00:00.000000',
                `Notes`         TEXT NULL,
                PRIMARY KEY (`Id`),
                INDEX `IX_SpoilageRecords_ProductId` (`ProductId`),
                INDEX `IX_SpoilageRecords_RecordedAt` (`RecordedAt`)
            ) CHARACTER SET utf8mb4;
        ");

        await DbSeeder.SeedRolesAndAdminAsync(services);
        await DbSeeder.SeedProductsAsync(db);
        await DbSeeder.SeedHistoricalDataAsync(db, services);
        await DbSeederExtensions.SeedSupportingDataAsync(db, services);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seeding.");
        logger.LogWarning("⚠️  Database seeding failed. Check your connection string.");
    }
}

await app.RunAsync();