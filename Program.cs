using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HanaMedia.Middlewares;
using HanaMedia.Models;
using HanaMedia.Services;
using HanaMedia.Services.Accounts;
using HanaMedia.Services.Auditing;
using HanaMedia.Services.Config;
using HanaMedia.Services.Dashboard;
using HanaMedia.Services.Security;
using HanaMedia.Services.Tasks;
using HanaMedia.Services.Ideas;

const string BootstrapAdminArgument = "--bootstrap-admin";
var bootstrapAdminRequested = args.Any(argument =>
    string.Equals(argument, BootstrapAdminArgument, StringComparison.OrdinalIgnoreCase));
var applicationArguments = args
    .Where(argument =>
        !string.Equals(argument, BootstrapAdminArgument, StringComparison.OrdinalIgnoreCase))
    .ToArray();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = applicationArguments,
    ContentRootPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot")
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AccountService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EmployeeAvatarService>();
// Read-only compatibility for existing Identity hashes. New/reset passwords stay SHA-256.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAccountPasswordService, AccountPasswordService>();
builder.Services.AddScoped<ISystemAuditService, SystemAuditService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IAdminITDashboardService, AdminITDashboardService>();
builder.Services.AddScoped<IDirectorMonitoringService, DirectorMonitoringService>();
builder.Services.AddScoped<IAccountManagementService, AccountManagementService>();
builder.Services.AddScoped<IWorkTaskService, WorkTaskService>();
builder.Services.AddScoped<IIdeaService, IdeaService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IClientIpResolver, ClientIpResolver>();
builder.Services.AddScoped<IIpAccessControlService, IpAccessControlService>();
builder.Services.AddSingleton<ISystemConfigService, SystemConfigService>();
builder.Services.AddScoped<DevelopmentAdminBootstrapper>();
builder.Services.AddScoped<AccountCookieEvents>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "HanaMedia.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AccountCookieEvents);
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-patch database for Module 11 contract workflow columns if missing
using (var dbScope = app.Services.CreateScope())
{
    var dbContext = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var columnsToAdd = new Dictionary<string, string>
    {
        { "contract_status", "ALTER TABLE bookings ADD contract_status NVARCHAR(50) NULL DEFAULT 'cho_duyet';" },
        { "contract_approved_at", "ALTER TABLE bookings ADD contract_approved_at DATETIME2 NULL;" },
        { "contract_approved_by_id", "ALTER TABLE bookings ADD contract_approved_by_id INT NULL;" },
        { "contract_signed_at", "ALTER TABLE bookings ADD contract_signed_at DATETIME2 NULL;" },
        { "contract_signed_by_id", "ALTER TABLE bookings ADD contract_signed_by_id INT NULL;" },
        { "rejection_reason", "ALTER TABLE bookings ADD rejection_reason NVARCHAR(1000) NULL;" }
    };

    foreach (var kvp in columnsToAdd)
    {
        try
        {
            var checkSql = $"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('bookings') AND name = '{kvp.Key}') BEGIN {kvp.Value} END";
            dbContext.Database.ExecuteSqlRaw(checkSql);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Auto-patch column {Col}: {Msg}", kvp.Key, ex.Message);
        }
    }

    try
    {
        dbContext.Database.ExecuteSqlRaw("UPDATE bookings SET contract_status = 'cho_duyet' WHERE contract_status IS NULL;");
    }
    catch { }

    // Auto-patch ip_access_rules table
    try
    {
        var createIpTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ip_access_rules')
            BEGIN
                CREATE TABLE ip_access_rules (
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    cidr VARCHAR(100) NOT NULL,
                    rule_type VARCHAR(20) NOT NULL DEFAULT 'Allow',
                    is_enabled BIT NOT NULL DEFAULT 1,
                    description NVARCHAR(255) NULL,
                    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
                    updated_at DATETIME2 NULL,
                    created_by NVARCHAR(100) NULL,
                    updated_by NVARCHAR(100) NULL
                );
            END";
        dbContext.Database.ExecuteSqlRaw(createIpTableSql);

        // Seed initial Whitelist from appsettings.json if table is completely empty
        var ruleCount = dbContext.IpAccessRules.Count();
        if (ruleCount == 0)
        {
            var initialCidrsConfig = app.Configuration["AllowedNetworkCidr"] ?? "192.168.110.0/24, 192.168.100.0/24, 192.168.0.0/24, 10.33.0.0/16";
            var initialCidrs = initialCidrsConfig
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var rawCidr in initialCidrs)
            {
                if (HanaMedia.Services.Security.CidrHelper.IsValidCidrOrIp(rawCidr, out _, out var normalized))
                {
                    dbContext.IpAccessRules.Add(new HanaMedia.Models.IpAccessRule
                    {
                        Cidr = normalized,
                        RuleType = HanaMedia.Models.IpRuleType.Allow,
                        IsEnabled = true,
                        Description = "Cấu hình mặc định từ appsettings.json",
                        CreatedAt = DateTime.Now,
                        CreatedBy = "SystemSeed"
                    });
                }
            }
            dbContext.SaveChanges();
            app.Logger.LogInformation("Successfully seeded initial IP whitelist rules into database.");
        }

        // Emergency cleanup: Remove accidental Deny rules for 10.33.0.0/16 and restore Whitelist
        dbContext.Database.ExecuteSqlRaw("DELETE FROM ip_access_rules WHERE cidr LIKE '10.33%' AND rule_type = 'Deny';");
        var hasAllow1033 = dbContext.IpAccessRules.Any(r => r.Cidr == "10.33.0.0/16" && r.RuleType == "Allow");
        if (!hasAllow1033)
        {
            dbContext.IpAccessRules.Add(new HanaMedia.Models.IpAccessRule
            {
                Cidr = "10.33.0.0/16",
                RuleType = HanaMedia.Models.IpRuleType.Allow,
                IsEnabled = true,
                Description = "Khôi phục dải IP mạng quản trị",
                CreatedAt = DateTime.Now,
                CreatedBy = "EmergencyRecovery"
            });
            dbContext.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Auto-patch/seed ip_access_rules: {Msg}", ex.Message);
    }
}

if (bootstrapAdminRequested)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Lệnh --bootstrap-admin chỉ được phép chạy trong môi trường Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var bootstrapCredentials =
        DevelopmentAdminBootstrapper.ReadConfiguration(app.Configuration);
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<DevelopmentAdminBootstrapper>();
    var result = await bootstrapper.RunAsync(bootstrapCredentials);
    app.Logger.LogInformation("{BootstrapResult}", result.Message);
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    // Development: middleware bắt exception & trả JSON chuẩn cho API
    app.UseGlobalExceptionHandler();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Lắng nghe trên tất cả IP (để test từ máy khác trong mạng LAN)
app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5028");

app.Run();
